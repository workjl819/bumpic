namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 已验证并规范化的 Apple App Store 服务端通知外层信息。
/// </summary>
public record AppleStoreNotificationEnvelope(
    string ExternalNotificationId,
    string NotificationType,
    int SchemaVersion,
    int ParserVersion,
    string NormalizedPayload,
    DateTimeOffset OccurredAt,
    string SourcePrincipal,
    bool BelongsToCurrentDeployment);
