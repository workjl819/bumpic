namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Google Play Pub/Sub Push 通知解析端口。
/// </summary>
public interface IGooglePlayNotificationParser
{
    GooglePlayNotificationEnvelope Parse(string messageId, string data, DateTimeOffset? publishTime);
}
