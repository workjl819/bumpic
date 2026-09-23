namespace Bumpic.Web.Options;

/// <summary>
/// Apple App Store Server API JWT 配置。
/// </summary>
public class AppleStoreServerApiOptions
{
    /// <summary>
    /// App Store Connect 的 Issuer ID。
    /// </summary>
    public string IssuerId { get; set; } = string.Empty;

    /// <summary>
    /// App Store Server API 私钥的 Key ID。
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// App Store Server API 私钥（.p8 PEM）内容，由部署配置或密钥注入提供，禁止提交到仓库。
    /// </summary>
    public string PrivateKeyPem { get; set; } = string.Empty;
}
