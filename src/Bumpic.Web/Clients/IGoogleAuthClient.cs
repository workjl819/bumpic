using Refit;

namespace Bumpic.Web.Clients;

/// <summary>
/// Google 认证相关第三方接口客户端（BaseAddress: https://www.googleapis.com）。
/// </summary>
public interface IGoogleAuthClient
{
    /// <summary>
    /// 获取 Google 公钥 JWKS（GET /oauth2/v3/certs）。
    /// </summary>
    [Get("/oauth2/v3/certs")]
    Task<string> GetCertsAsync(CancellationToken cancellationToken);
}
