using System.Text.Json;
using Microsoft.Extensions.Options;
using Bumpic.Web.Options;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Google Play RTDN Pub/Sub Push 通知解析器。
/// </summary>
public class GooglePlayNotificationParser(IOptions<GooglePlayOptions> options) : IGooglePlayNotificationParser
{
    private const int SchemaVersion = 1;
    private const int ParserVersion = 1;

    /// <inheritdoc />
    public GooglePlayNotificationEnvelope Parse(string messageId, string data, DateTimeOffset? publishTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        try
        {
            using var payloadDocument = JsonDocument.Parse(Convert.FromBase64String(data));
            var payload = payloadDocument.RootElement;
            var packageName = ReadOptionalString(element: payload, propertyName: "packageName");
            var occurredAt = ReadOptionalInt64(element: payload, propertyName: "eventTimeMillis") is { } value
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : publishTime ?? DateTimeOffset.UtcNow;
            var notificationType = ResolveNotificationType(payload: payload);
            var normalizedPayload = JsonSerializer.Serialize(new
            {
                schemaVersion = SchemaVersion,
                parserVersion = ParserVersion,
                messageId,
                publishTime,
                notificationType,
                payload
            });
            return new GooglePlayNotificationEnvelope(
                ExternalNotificationId: messageId.Trim(),
                NotificationType: notificationType,
                SchemaVersion: SchemaVersion,
                ParserVersion: ParserVersion,
                NormalizedPayload: normalizedPayload,
                OccurredAt: occurredAt,
                BelongsToCurrentDeployment: string.IsNullOrWhiteSpace(packageName)
                                            || string.Equals(
                                                packageName,
                                                options.Value.PackageName,
                                                StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new StoreClientException(
                code: "STORE_NOTIFICATION_INVALID",
                isRetryable: false,
                message: "Google Pub/Sub 通知数据不是合法 Base64 JSON。",
                innerException: exception);
        }
    }

    private static string ResolveNotificationType(JsonElement payload)
    {
        if (payload.TryGetProperty("testNotification", out _))
        {
            return "TEST";
        }

        if (payload.TryGetProperty("voidedPurchaseNotification", out _))
        {
            return "VOIDED_PURCHASE";
        }

        if (payload.TryGetProperty("pendingRefundReviewNotification", out _))
        {
            return "PENDING_REFUND_REVIEW";
        }

        if (payload.TryGetProperty("oneTimeProductNotification", out var oneTime)
            && oneTime.TryGetProperty("notificationType", out var type)
            && type.TryGetInt32(out var value))
        {
            return value switch
            {
                1 => "ONE_TIME_PRODUCT_PURCHASED",
                2 => "ONE_TIME_PRODUCT_CANCELED",
                _ => $"ONE_TIME_PRODUCT_UNKNOWN_{value}"
            };
        }

        return "UNKNOWN";
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? ReadOptionalInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numeric))
        {
            return numeric;
        }

        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var textValue)
            ? textValue
            : null;
    }
}
