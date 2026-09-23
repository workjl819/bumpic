using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// 平台授权撤销端口：只在 Endpoint 或集成事件处理器中调用，不进入 Command 事务。
/// </summary>
public interface IExternalIdentityRevocationService
{
    /// <summary>
    /// 用 Apple 授权码换取 refresh token（供宽限期届满后撤销使用）。
    /// </summary>
    /// <param name="authorizationCode">客户端 Sign in with Apple 返回的授权码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>refresh token；换取失败或未配置时为 null。</returns>
    Task<string?> ExchangeAppleRefreshTokenAsync(string authorizationCode, CancellationToken cancellationToken);

    /// <summary>
    /// 调用 Apple 撤销接口使该用户的授权失效。
    /// </summary>
    /// <param name="refreshToken">此前换取的 refresh token 明文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>撤销成功返回 true；令牌已失效视为已撤销并返回 true；其余失败返回 false。</returns>
    Task<bool> RevokeAppleAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// 调用 Google 撤销接口使客户端提供的 access token 失效。
    /// </summary>
    /// <param name="accessToken">客户端 Google 登录得到的 access token。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>撤销成功返回 true；令牌已失效视为已撤销并返回 true；其余失败返回 false。</returns>
    Task<bool> RevokeGoogleAsync(string accessToken, CancellationToken cancellationToken);
}

/// <summary>
/// 基于 HTTP 的平台撤销实现；所有失败只记录日志并返回结果，不抛出业务异常。
/// </summary>
public class ExternalIdentityRevocationService(
    IHttpClientFactory httpClientFactory,
    IOptions<AppleExternalIdentityOptions> appleOptions,
    ILogger<ExternalIdentityRevocationService> logger) : IExternalIdentityRevocationService
{
    /// <summary>
    /// Apple 授权服务器客户端名称。
    /// </summary>
    public const string AppleHttpClientName = "ExternalIdentity.Apple";

    /// <summary>
    /// Google OAuth 客户端名称。
    /// </summary>
    public const string GoogleOAuthHttpClientName = "ExternalIdentity.GoogleOAuth";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<string?> ExchangeAppleRefreshTokenAsync(string authorizationCode, CancellationToken cancellationToken)
    {
        var options = appleOptions.Value;
        if (string.IsNullOrWhiteSpace(authorizationCode)
            || string.IsNullOrWhiteSpace(options.TeamId)
            || string.IsNullOrWhiteSpace(options.KeyId)
            || string.IsNullOrWhiteSpace(options.ClientId)
            || string.IsNullOrWhiteSpace(options.AuthKeyPrivateKeyPem))
        {
            logger.LogWarning("Apple 撤销令牌换取未执行：授权码或外部身份配置不完整");
            return null;
        }

        try
        {
            var clientSecret = AppleClientSecretGenerator.Generate(
                options.TeamId,
                options.KeyId,
                options.ClientId,
                options.AuthKeyPrivateKeyPem);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = clientSecret,
                ["code"] = authorizationCode,
                ["grant_type"] = "authorization_code"
            });

            var client = httpClientFactory.CreateClient(AppleHttpClientName);
            using var response = await client.PostAsync("/auth/token", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Apple 授权码换取失败：{StatusCode} {Body}", (int)response.StatusCode, body);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<AppleTokenResponse>(JsonOptions, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload?.RefreshToken))
            {
                logger.LogWarning("Apple 授权码换取成功但未返回 refresh_token");
                return null;
            }

            return payload.RefreshToken;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Apple 授权码换取异常");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAppleAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var options = appleOptions.Value;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.TeamId)
            || string.IsNullOrWhiteSpace(options.KeyId)
            || string.IsNullOrWhiteSpace(options.ClientId)
            || string.IsNullOrWhiteSpace(options.AuthKeyPrivateKeyPem))
        {
            logger.LogWarning("Apple 撤销未执行：外部身份配置不完整");
            return false;
        }

        try
        {
            var clientSecret = AppleClientSecretGenerator.Generate(
                options.TeamId,
                options.KeyId,
                options.ClientId,
                options.AuthKeyPrivateKeyPem);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = clientSecret,
                ["token"] = refreshToken,
                ["token_type_hint"] = "refresh_token"
            });

            var client = httpClientFactory.CreateClient(AppleHttpClientName);
            using var response = await client.PostAsync("/auth/revoke", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            // 令牌已失效（invalid_grant 等）视为已撤销，避免阻塞账户注销。
            logger.LogWarning("Apple 撤销返回失败：{StatusCode} {Body}", (int)response.StatusCode, body);
            return body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Apple 撤销异常");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevokeGoogleAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return true;
        }

        try
        {
            var client = httpClientFactory.CreateClient(GoogleOAuthHttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = accessToken
            });

            using var response = await client.PostAsync("/revoke", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Google 撤销返回失败：{StatusCode} {Body}", (int)response.StatusCode, body);
            return response.StatusCode == System.Net.HttpStatusCode.BadRequest
                && body.Contains("invalid_token", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Google 撤销异常");
            return false;
        }
    }

    /// <summary>
    /// Apple 令牌接口响应。
    /// </summary>
    private sealed record AppleTokenResponse(
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
