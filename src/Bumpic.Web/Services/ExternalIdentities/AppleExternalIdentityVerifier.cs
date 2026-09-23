using Microsoft.Extensions.Options;

namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// Apple 身份令牌验证器：拉取 Apple JWKS 校验 RS256 签名、iss、aud、exp、sub 与 nonce。
/// </summary>
public class AppleExternalIdentityVerifier(
    IOptions<AppleExternalIdentityOptions> options,
    PlatformJwksProvider jwksProvider,
    ILogger<AppleExternalIdentityVerifier> logger)
{
    /// <summary>
    /// 验证 Apple identityToken。
    /// </summary>
    /// <param name="identityToken">Sign in with Apple 返回的 identityToken（JWS 紧凑格式）。</param>
    /// <param name="nonce">
    /// 客户端自己生成、并交给 Apple SDK 的原始 nonce（客户端传给 Apple 的是它的 SHA-256 摘要，故两者都接受）。
    /// </param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>验证后的外部身份主体。</returns>
    /// <exception cref="KnownException"><c>EXTERNAL_CREDENTIAL_INVALID</c>：校验失败。</exception>
    public virtual async Task<UserExternalIdentityPrincipal> VerifyAsync(
        string identityToken,
        string? nonce,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(identityToken))
        {
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }

        if (config.RequireNonce && string.IsNullOrWhiteSpace(nonce))
        {
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }

        var requirements = new IdTokenValidationRequirements(
            AllowedIssuers: config.AllowedIssuers,
            AllowedAudiences: config.AllowedAudiences,
            AllowedAlgorithms: config.AllowedAlgorithms,
            ExpectedNonce: nonce,
            AllowHashedNonce: true);

        var claims = await ValidateWithKeyRefreshAsync(identityToken, requirements, cancellationToken);

        if (claims.Email is not null && !claims.EmailVerified)
        {
            // 令牌带了邮箱但未声明已验证：不参与账户匹配，端点会返回 EXTERNAL_IDENTITY_EMAIL_REQUIRED。
            logger.LogWarning("Apple 令牌邮箱未通过 email_verified 校验，不参与账户匹配");
        }
        else if (claims.Email is null)
        {
            // Apple 只在首次授权返回邮箱：未绑定身份又拿不到邮箱时只能回退到邮箱验证码登录。
            logger.LogWarning("Apple 令牌未包含邮箱声明，未绑定身份无法自动完成注册或绑定");
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
        string identityToken,
        IdTokenValidationRequirements requirements,
        CancellationToken cancellationToken)
    {
        var jwks = await FetchJwksAsync(forceRefresh: false, cancellationToken);
        try
        {
            return ExternalIdTokenValidator.Validate(identityToken, jwks, requirements);
        }
        catch (KnownException exception) when (exception.Message == ExternalIdTokenErrorCodes.KeyNotFound)
        {
            logger.LogInformation(exception, "Apple JWKS 未命中 kid，强制刷新后重试");
            var refreshed = await FetchJwksAsync(forceRefresh: true, cancellationToken);
            try
            {
                return ExternalIdTokenValidator.Validate(identityToken, refreshed, requirements);
            }
            catch (KnownException retryException)
            {
                logger.LogWarning(retryException, "Apple 身份令牌校验失败：{Reason}", retryException.Message);
                throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
            }
        }
        catch (KnownException exception)
        {
            logger.LogWarning(exception, "Apple 身份令牌校验失败：{Reason}", exception.Message);
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }
    }

    /// <summary>
    /// 拉取 Apple JWKS，失败统一转为业务错误码。
    /// </summary>
    private async Task<string> FetchJwksAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        try
        {
            return await jwksProvider.GetAppleJwksAsync(cancellationToken, forceRefresh);
        }
        catch (KnownException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "拉取 Apple JWKS 失败");
            throw new KnownException(ExternalIdTokenErrorCodes.Invalid);
        }
    }
}