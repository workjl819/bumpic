using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StoreNotification;
using Bumpic.Web.Clients.Store;

namespace Bumpic.Web.Endpoints.StoreNotification;

/// <summary>
/// Apple App Store 服务端通知请求。
/// </summary>
public record HandleAppleStoreNotificationRequest(string SignedPayload);

/// <summary>
/// 验证并持久化 Apple App Store 服务端通知。
/// </summary>
[HttpPost("/api/v1/store-notification/apple/handle")]
[AllowAnonymous]
[Tags("StoreNotification")]
public class HandleAppleStoreNotificationEndpoint(
    IMediator mediator,
    IAppleStoreNotificationParser parser,
    IClock clock,
    ILogger<HandleAppleStoreNotificationEndpoint> logger)
    : Endpoint<HandleAppleStoreNotificationRequest, EmptyResponse>
{
    private const int InvalidNotificationStatusCode = 400;

    /// <summary>
    /// 来源验证和 inbox 持久化完成后快速 ACK，业务处理留给后台 Worker。
    /// </summary>
    public override async Task HandleAsync(
        HandleAppleStoreNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var signedPayload = request.SignedPayload ?? string.Empty;
        var rawPayload = JsonSerializer.Serialize(request);
        logger.LogInformation(
            "Received Apple store notification. PayloadLength={PayloadLength}",
            signedPayload.Length);

        AppleStoreNotificationEnvelope notification;
        try
        {
            notification = await parser.ParseAsync(
                signedPayload: signedPayload,
                cancellationToken: cancellationToken);
        }
        catch (StoreClientException exception) when (!exception.IsRetryable)
        {
            logger.LogWarning("Apple store notification signature or schema validation failed: {Code}", exception.Code);
            await Send.ResponseAsync(
                new EmptyResponse(),
                statusCode: InvalidNotificationStatusCode,
                cancellation: cancellationToken);
            return;
        }

        if (!notification.BelongsToCurrentDeployment)
        {
            logger.LogError(
                "Verified Apple store notification does not belong to this deployment. NotificationId={NotificationId}",
                notification.ExternalNotificationId);
            await Send.NoContentAsync(cancellation: cancellationToken);
            return;
        }

        logger.LogInformation(
            "Verified Apple store notification. NotificationId={NotificationId}, NotificationType={NotificationType}",
            notification.ExternalNotificationId,
            notification.NotificationType);

        await mediator.Send(new ReceiveStoreNotificationCommand(
            Store: AppStore.AppleAppStore,
            ExternalNotificationId: notification.ExternalNotificationId,
            NotificationType: notification.NotificationType,
            SchemaVersion: notification.SchemaVersion,
            ParserVersion: notification.ParserVersion,
            RawPayload: rawPayload,
            NormalizedPayload: notification.NormalizedPayload,
            SourceVerifiedAt: clock.UtcNow,
            SourcePrincipal: notification.SourcePrincipal,
            SourceAudience: null,
            OccurredAt: notification.OccurredAt), cancellationToken);
        await Send.NoContentAsync(cancellation: cancellationToken);
    }
}
