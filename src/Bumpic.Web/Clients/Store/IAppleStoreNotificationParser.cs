namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Apple App Store 服务端通知验证与规范化解析器。
/// </summary>
public interface IAppleStoreNotificationParser
{
    /// <summary>
    /// 验证并规范化 Apple App Store 服务端通知。
    /// </summary>
    Task<AppleStoreNotificationEnvelope> ParseAsync(
        string signedPayload,
        CancellationToken cancellationToken);
}
