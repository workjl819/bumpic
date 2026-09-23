namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// Apple 外部身份验证配置。
/// </summary>
public class AppleExternalIdentityOptions
{
    /// <summary>
    /// Sign in with Apple AuthKey 的 .p8 私钥内容，由部署密钥注入提供。
    /// </summary>
    public string AuthKeyPrivateKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// Apple 开发者团队标识（生成 client_secret 需要）。
    /// </summary>
    public string TeamId { get; set; } = string.Empty;

    /// <summary>
    /// Sign in with Apple 所用 AuthKey 的 Key ID（生成 client_secret 需要）。
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// 调用 Apple 令牌与撤销接口时使用的 client_id（iOS 为 Bundle ID，Web 流程为 Service ID）。
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 允许的签发方。
    /// </summary>
    public IReadOnlyList<string> AllowedIssuers { get; set; } = new[] { "https://appleid.apple.com" };

    /// <summary>
    /// 允许的受众列表（iOS Bundle ID）。
    /// </summary>
    public IReadOnlyList<string> AllowedAudiences { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 允许的签名算法；实测 Apple /auth/keys 发布 RSA/RS256 公钥，identityToken 使用 RS256。
    /// </summary>
    public IReadOnlyList<string> AllowedAlgorithms { get; set; } = new[] { "RS256" };

    /// <summary>
    /// 是否必须校验 nonce；为 true 时请求必须携带与 Token nonce 声明一致的 nonce。
    /// </summary>
    public bool RequireNonce { get; set; } = true;
}

/// <summary>
/// Google 外部身份验证配置。
/// </summary>
public class GoogleExternalIdentityOptions
{
    /// <summary>
    /// 允许的签发方；Google 可能返回带或不带协议前缀两种写法。
    /// </summary>
    public IReadOnlyList<string> AllowedIssuers { get; set; } = new[] { "accounts.google.com", "https://accounts.google.com" };

    /// <summary>
    /// 允许的受众（OAuth Client ID）列表。客户端设置了 serverClientId 时，idToken 的 aud 是该 serverClientId
    /// （通常是 Web Client ID），azp 才是平台自身的 Client ID，因此白名单需要同时包含 iOS、Android 与 Web 三个 Client ID。
    /// </summary>
    public IReadOnlyList<string> AllowedAudiences { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 允许的签名算法；Google ID Token 使用 RS256。
    /// </summary>
    public IReadOnlyList<string> AllowedAlgorithms { get; set; } = new[] { "RS256" };

    /// <summary>
    /// Google OAuth 端点基地址，用于撤销授权。
    /// </summary>
    public string OAuthBaseUrl { get; set; } = "https://oauth2.googleapis.com";
}
