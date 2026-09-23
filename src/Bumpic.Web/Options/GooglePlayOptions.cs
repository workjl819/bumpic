namespace Bumpic.Web.Options;

/// <summary>
/// Google Play Developer API 验单部署配置。
/// </summary>
public class GooglePlayOptions
{
    /// <summary>
    /// 当前部署唯一允许的 Google Play 包名。
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Play Developer API 服务账号 JSON 密钥内容，由部署配置或密钥注入提供，禁止提交到仓库。
    /// </summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>
    /// Play Developer API 根地址，测试环境可指向本地替身服务。
    /// </summary>
    public string ApiBaseAddress { get; set; } = "https://androidpublisher.googleapis.com/androidpublisher/v3/";

    /// <summary>
    /// 商店调用超时秒数。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Pub/Sub Push OIDC 令牌的目标受众。
    /// </summary>
    public string PushAudience { get; set; } = string.Empty;

    /// <summary>
    /// 被授权投递 Pub/Sub Push 的 Google 服务账号邮箱。
    /// </summary>
    public string PushServiceAccountEmail { get; set; } = string.Empty;

    /// <summary>
    /// Google OIDC 颁发者地址。
    /// </summary>
    public string PushOidcAuthority { get; set; } = "https://accounts.google.com";
}
