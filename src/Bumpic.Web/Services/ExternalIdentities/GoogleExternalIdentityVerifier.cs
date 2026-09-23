using Microsoft.Extensions.Options;

namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// Google 身份令牌验证器：拉取 Google JWKS 校验 RS256 签名、iss、aud、exp 与 sub。
/// </summary>
public class GoogleExternalIdentityVerifier(
    IOptions<GoogleExternalIdentityOptions> options,
    PlatformJwksProvider jwksProvider,
    ILogger<GoogleExternalIdentityVerifier> logger)
{
    /// <summary>
    /// 验证 Google ID Token。
    /// </summary>
    /// <param name="idToken">Google Sign-In 返回的 ID Token（JWS 紧凑格式）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>验证后的外部身份主体。</returns>
    /// <exception cref="KnownException"><c>EXTERNAL_CREDENTIAL_INVALID</c>：校验失败。</exception>
    public virtual async Task<UserExternalIdentityPrincipal> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }

        var requirements = new IdTokenValidationRequirements(
            AllowedIssuers: config.AllowedIssuers,
            AllowedAudiences: config.AllowedAudiences,
            AllowedAlgorithms: config.AllowedAlgorithms,
            ExpectedNonce: null);

        var claims = await ValidateWithKeyRefreshAsync(idToken, requirements, cancellationToken);

        if (claims.Email is not null && !claims.EmailVerified)
        {
            // 令牌带了邮箱但未声明已验证：不参与账户匹配，端点会返回 EXTERNAL_IDENTITY_EMAIL_REQUIRED。
            logger.LogWarning("Google 令牌邮箱未通过 email_verified 校验，不参与账户匹配");
        }
        else if (claims.Email is null)
        {
            // Google 只在首次授权返回邮箱：未绑定身份又拿不到邮箱时只能回退到邮箱验证码登录。
            logger.LogWarning("Google 令牌未包含邮箱声明，未绑定身份无法自动完成注册或绑定");
        }

        // 只有令牌声明 email_verified 时才把邮箱交给上层：上层会据此匹配或创建邮箱账户。
        return new UserExternalIdentityPrincipal(
            claims.Subject,
            claims.EmailVerified ? claims.Email : null,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 校验令牌；JWKS 未命中 kid 时强制刷新一次后重试。
    /// </summary>
    private async Task<IdTokenClaims> ValidateWithKeyRefreshAsync(
        string idToken,
        IdTokenValidationRequirements requirements,
        CancellationToken cancellationToken)
    {
        var jwks = await FetchJwksAsync(forceRefresh: false, cancellationToken);
        try
        {
            return ExternalIdTokenValidator.Validate(idToken, jwks, requirements);
        }
        catch (KnownException exception) when (exception.Message == ExternalIdTokenErrorCodes.KeyNotFound)
        {
            logger.LogInformation(exception, "Google JWKS 未命中 kid，强制刷新后重试");
            var refreshed = await FetchJwksAsync(forceRefresh: true, cancellationToken);
            try
            {
                return ExternalIdTokenValidator.Validate(idToken, refreshed, requirements);
            }
            catch (KnownException retryException)
            {
                logger.LogWarning(retryException, "Google 身份令牌校验失败：{Reason}", retryException.Message);
                throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
            }
        }
        catch (KnownException exception)
        {
            logger.LogWarning(exception, "Google 身份令牌校验失败：{Reason}", exception.Message);
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }
    }

    /// <summary>
    /// 拉取 Google JWKS，失败统一转为业务错误码。
    /// </summary>
    private async Task<string> FetchJwksAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        try
        {
            return await jwksProvider.GetGoogleJwksAsync(cancellationToken, forceRefresh);
        }
        catch (KnownException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "拉取 Google JWKS 失败");
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }
    }
}