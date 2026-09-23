using Refit;

namespace Bumpic.Web.Clients;

/// <summary>
/// Apple 认证相关第三方接口客户端（BaseAddress: https://appleid.apple.com）。
/// </summary>
public interface IAppleAuthClient
{
    /// <summary>
    /// 获取 Apple 公钥 JWKS（GET /auth/keys）。
    /// </summary>
    [Get("/auth/keys")]
    Task<string> GetJwksAsync(CancellationToken cancellationToken);
}
