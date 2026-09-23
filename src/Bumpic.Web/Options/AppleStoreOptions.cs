namespace Bumpic.Web.Options;

/// <summary>
/// Apple App Store 验单部署配置。
/// </summary>
public class AppleStoreOptions
{
    /// <summary>
    /// 当前部署唯一允许的 Apple Bundle ID。
    /// </summary>
    public string BundleId { get; set; } = string.Empty;

    /// <summary>
    /// 当前部署的 Apple App Apple ID，可选，用于服务端通知部署校验。
    /// </summary>
    public long? AppAppleId { get; set; }

    /// <summary>
    /// 当前部署允许的 Apple 支付环境名称，只允许 Sandbox 或 Production。
    /// </summary>
    public string Environment { get; set; } = "Sandbox";

    /// <summary>
    /// Apple 根证书 PEM 列表，用于校验 JWS x5c 证书链。
    /// </summary>
    public List<string> RootCertificatesPem { get; set; } = [];

    /// <summary>
    /// 是否联网检查 Apple 签名证书吊销状态；实时验单和通知应保持启用。
    /// </summary>
    public bool EnableOnlineChecks { get; set; } = true;

    /// <summary>
    /// Apple App Store Server API 凭据配置。
    /// </summary>
    public AppleStoreServerApiOptions AppStoreServerApi { get; set; } = new();
}
