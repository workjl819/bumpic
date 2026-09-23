using Hangfire;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StoreNotification;
using Bumpic.Web.Application.Queries.StoreNotification;
using Bumpic.Web.Services.Store;

namespace Bumpic.Web.Application.Jobs;

/// <summary>
/// 使用数据库租约处理 durable inbox 中的商店通知。
/// </summary>
public class ProcessStoreNotificationInboxJob(
    IMediator mediator,
    IStoreNotificationProcessor processor,
    IClock clock,
    ILogger<ProcessStoreNotificationInboxJob> logger)
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 8;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 领取并处理一批已到期通知。
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var receiptIds = await mediator.Send(
            new GetPendingStoreNotificationIdsQuery(Take: BatchSize),
            cancellationToken);
        foreach (var receiptId in receiptIds)
        {
            await ProcessOneAsync(receiptId: receiptId, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessOneAsync(
        Domain.AggregateModel.StoreNotificationReceiptAggregate.StoreNotificationReceiptId receiptId,
        CancellationToken cancellationToken)
    {
        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var acquireResult = await mediator.Send(new AcquireStoreNotificationCommand(
            StoreNotificationReceiptId: receiptId,
            LeaseOwner: leaseOwner,
            LeaseDuration: LeaseDuration), cancellationToken);
        if (acquireResult != StoreNotificationReceiptAcquireResult.Acquired)
        {
            return;
        }

        try
        {
            await processor.ProcessAsync(receiptId: receiptId, cancellationToken: cancellationToken);
            await mediator.Send(new CompleteStoreNotificationCommand(
                StoreNotificationReceiptId: receiptId,
                LeaseOwner: leaseOwner), cancellationToken);
        }
        catch (StoreNotificationProcessingException exception)
        {
            logger.LogError(
                exception,
                "商店通知处理失败。ReceiptId={ReceiptId}, Code={Code}, Retryable={Retryable}",
                receiptId,
                exception.Code,
                exception.IsRetryable);
            await FailAsync(
                receiptId: receiptId,
                leaseOwner: leaseOwner,
                failureCode: exception.Code,
                forceDeadLetter: !exception.IsRetryable,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "商店通知处理发生未分类异常。ReceiptId={ReceiptId}", receiptId);
            await FailAsync(
                receiptId: receiptId,
                leaseOwner: leaseOwner,
                failureCode: "STORE_NOTIFICATION_PROCESSING_FAILED",
                forceDeadLetter: false,
                cancellationToken: cancellationToken);
        }
    }

    private async Task FailAsync(
        Domain.AggregateModel.StoreNotificationReceiptAggregate.StoreNotificationReceiptId receiptId,
        string leaseOwner,
        string failureCode,
        bool forceDeadLetter,
        CancellationToken cancellationToken)
    {
        var receipt = await mediator.Send(
            new GetStoreNotificationReceiptQuery(StoreNotificationReceiptId: receiptId),
            cancellationToken);
        var attemptCount = receipt?.AttemptCount ?? MaxAttempts;
        var deadLetter = forceDeadLetter || attemptCount >= MaxAttempts;
        var delaySeconds = Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        await mediator.Send(new FailStoreNotificationCommand(
            StoreNotificationReceiptId: receiptId,
            LeaseOwner: leaseOwner,
            FailureCode: failureCode,
            NextRetryAt: deadLetter ? null : clock.UtcNow.AddSeconds(delaySeconds),
            DeadLetter: deadLetter), cancellationToken);
    }
}
