using System.Text.Json;
using Microsoft.Extensions.Options;
using Bumpic.Web.Options;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Apple App Store Server Notifications V2 解析器。
/// </summary>
public class AppleStoreNotificationParser(
    IAppleSignedPayloadVerifier signedPayloadVerifier,
    IOptions<AppleStoreOptions> options) : IAppleStoreNotificationParser
{
    private const int SchemaVersion = 2;
    private const int ParserVersion = 1;

    /// <inheritdoc />
    public async Task<AppleStoreNotificationEnvelope> ParseAsync(
        string signedPayload,
        CancellationToken cancellationToken)
    {
        var verified = await signedPayloadVerifier.VerifyNotificationPayloadAsync(
            signedPayload: signedPayload,
            cancellationToken: cancellationToken);
        var payload = verified.Payload;
        var notificationType = ReadOptionalString(payload, "notificationType") ?? "UNKNOWN";
        var notificationId = ReadOptionalString(payload, "notificationUUID")
                             ?? $"APPLE_UNPARSED:{verified.PayloadHash}";
        var occurredAt = ReadTimestamp(payload, "signedDate") ?? DateTimeOffset.UtcNow;
        var belongsToCurrentDeployment = MatchesDeployment(
            payload: payload,
            configuration: options.Value);
        var normalizedPayload = belongsToCurrentDeployment
            ? JsonSerializer.Serialize(new
            {
                schemaVersion = SchemaVersion,
                parserVersion = ParserVersion,
                notificationType,
                notificationUUID = notificationId,
                signedDate = occurredAt,
                payload
            })
            : string.Empty;

        return new AppleStoreNotificationEnvelope(
            ExternalNotificationId: notificationId,
            NotificationType: notificationType,
            SchemaVersion: SchemaVersion,
            ParserVersion: ParserVersion,
            NormalizedPayload: normalizedPayload,
            OccurredAt: occurredAt,
            SourcePrincipal: verified.SourcePrincipal,
            BelongsToCurrentDeployment: belongsToCurrentDeployment);
    }

    private static bool MatchesDeployment(JsonElement payload, AppleStoreOptions configuration)
    {
        if (!payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var bundleId = ReadOptionalString(data, "bundleId");
        var environment = ReadOptionalString(data, "environment");
        var appAppleId = ReadOptionalInt64(data, "appAppleId");
        var bundleMatches = string.IsNullOrWhiteSpace(bundleId)
                            || string.Equals(bundleId, configuration.BundleId, StringComparison.Ordinal);
        var environmentMatches = string.IsNullOrWhiteSpace(environment)
                                 || string.Equals(
                                     environment,
                                     AppleStoreEnvironmentResolver.ResolveName(
                                         environmentName: configuration.Environment),
                                     StringComparison.Ordinal);
        var appMatches = !configuration.AppAppleId.HasValue
                         || !appAppleId.HasValue
                         || appAppleId.Value == configuration.AppAppleId.Value;
        return bundleMatches && environmentMatches && appMatches;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? ReadOptionalInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string propertyName)
    {
        var milliseconds = ReadOptionalInt64(element: element, propertyName: propertyName);
        return milliseconds.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value)
            : null;
    }
}
