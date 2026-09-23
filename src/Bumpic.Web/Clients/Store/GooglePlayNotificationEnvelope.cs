namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 已解析的 Google Play Pub/Sub 通知信封。
/// </summary>
public record GooglePlayNotificationEnvelope(
    string ExternalNotificationId,
    string NotificationType,
    int SchemaVersion,
    int ParserVersion,
    string NormalizedPayload,
    DateTimeOffset OccurredAt,
    bool BelongsToCurrentDeployment);
