using System.Security.Claims;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StoreNotification;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Endpoints.StoreNotification;

/// <summary>
/// Google Pub/Sub Push 消息内容。
/// </summary>
public record GooglePubSubMessage(string MessageId, string Data, DateTimeOffset? PublishTime);

/// <summary>
/// Google Pub/Sub Push 请求。
/// </summary>
public record HandleGooglePlayNotificationRequest(GooglePubSubMessage Message, string? Subscription);

/// <summary>
/// 验证并持久化 Google Play 实时开发者通知。
/// </summary>
[HttpPost("/api/v1/store-notification/google/handle")]
[Authorize(AuthenticationSchemes = AuthenticationScheme)]
[Tags("StoreNotification")]
public class HandleGooglePlayNotificationEndpoint(
    IMediator mediator,
    IGooglePlayNotificationParser parser,
    IOptions<GooglePlayOptions> options,
    IClock clock,
    ILogger<HandleGooglePlayNotificationEndpoint> logger)
    : Endpoint<HandleGooglePlayNotificationRequest, EmptyResponse>
{
    /// <summary>
    /// Google Pub/Sub Push 专用认证方案。
    /// </summary>
    public const string AuthenticationScheme = "GooglePubSub";

    /// <summary>
    /// OIDC 来源验证和 inbox 持久化完成后快速 ACK。
    /// </summary>
    public override async Task HandleAsync(
        HandleGooglePlayNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var notificationData = request.Message.Data ?? string.Empty;
        var rawPayload = JsonSerializer.Serialize(request);
        var configuredEmail = options.Value.PushServiceAccountEmail;
        var callerEmail = User.FindFirstValue("email");
        var emailVerified = User.FindFirstValue("email_verified");
        logger.LogInformation(
            "Received Google Play store notification. CallerEmail={CallerEmail}, EmailVerified={EmailVerified}, MessageId={MessageId}, Subscription={Subscription}, DataLength={DataLength}",
            callerEmail,
            emailVerified,
            request.Message.MessageId,
            request.Subscription,
            notificationData.Length);

        if (string.IsNullOrWhiteSpace(configuredEmail)
            || !string.Equals(callerEmail, configuredEmail, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Google Pub/Sub Push service account identity is not allowed. CallerEmail={CallerEmail}, " +
                "EmailVerified={EmailVerified}, ConfiguredEmail={ConfiguredEmail}",
                callerEmail,
                emailVerified,
                configuredEmail);
            await Send.ResponseAsync(new EmptyResponse(), statusCode: 403, cancellation: cancellationToken);
            return;
        }

        GooglePlayNotificationEnvelope notification;
        try
        {
            notification = parser.Parse(
                messageId: request.Message.MessageId,
                data: notificationData,
                publishTime: request.Message.PublishTime);
        }
        catch (StoreClientException exception)
        {
            logger.LogWarning("Google store notification schema validation failed: {Code}", exception.Code);
            await Send.ResponseAsync(new EmptyResponse(), statusCode: 400, cancellation: cancellationToken);
            return;
        }

        if (!notification.BelongsToCurrentDeployment)
        {
            logger.LogError(
                "Verified Google store notification does not belong to this deployment. MessageId={MessageId}",
                notification.ExternalNotificationId);
            await Send.NoContentAsync(cancellation: cancellationToken);
            return;
        }

        logger.LogInformation(
            "Verified Google Play store notification. MessageId={MessageId}, NotificationType={NotificationType}",
            notification.ExternalNotificationId,
            notification.NotificationType);

        if (notification.NotificationType == "PENDING_REFUND_REVIEW")
        {
            logger.LogCritical(
                "Google purchase entered pending refund review and requires an operational response. MessageId={MessageId}",
                notification.ExternalNotificationId);
        }

        await mediator.Send(new ReceiveStoreNotificationCommand(
            Store: AppStore.GooglePlay,
            ExternalNotificationId: notification.ExternalNotificationId,
            NotificationType: notification.NotificationType,
            SchemaVersion: notification.SchemaVersion,
            ParserVersion: notification.ParserVersion,
            RawPayload: rawPayload,
            NormalizedPayload: notification.NormalizedPayload,
            SourceVerifiedAt: clock.UtcNow,
            SourcePrincipal: callerEmail!,
            SourceAudience: options.Value.PushAudience,
            OccurredAt: notification.OccurredAt), cancellationToken);
        await Send.NoContentAsync(cancellation: cancellationToken);
    }
}
