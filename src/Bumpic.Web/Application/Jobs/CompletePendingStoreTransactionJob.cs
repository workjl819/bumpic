using System.Security.Cryptography;
using System.Text;
using Hangfire;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransaction;
using Bumpic.Web.Application.Commands.StoreTransactionFact;
using Bumpic.Web.Application.Queries.StoreTransaction;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Application.Jobs;

/// <summary>
/// 补偿已到期的 Google Play 待消费交易。
/// </summary>
public class CompletePendingStoreTransactionJob(
    IMediator mediator,
    IGooglePlayClient googlePlayClient,
    IClock clock,
    ILogger<CompletePendingStoreTransactionJob> logger)
{
    private const int BatchSize = 50;
    private static readonly TimeSpan AlertThreshold = TimeSpan.FromHours(24);

    /// <summary>
    /// 查询权威购买状态，重试消费，并在成功后通过交易命令完成入账。
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var candidates = await mediator.Send(
            new GetDuePendingConsumptionStoreTransactionsQuery(Take: BatchSize),
            cancellationToken);
        foreach (var candidate in candidates)
        {
            await ProcessOneAsync(candidate: candidate, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessOneAsync(
        PendingConsumptionStoreTransactionResult candidate,
        CancellationToken cancellationToken)
    {
        if (clock.UtcNow - candidate.CreatedAt >= AlertThreshold)
        {
            logger.LogError(
                "Google Play 交易等待消费已超过 24 小时。StoreTransactionId={StoreTransactionId}, CreatedAt={CreatedAt}, AttemptCount={AttemptCount}",
                candidate.StoreTransactionId,
                candidate.CreatedAt,
                candidate.ConsumptionAttemptCount);
        }

        GooglePlayPurchase purchase;
        try
        {
            purchase = await googlePlayClient.GetPurchaseAsync(
                new GooglePlayPurchaseRequest(PurchaseToken: candidate.PurchaseToken),
                cancellationToken);
        }
        catch (StoreClientException exception)
        {
            await RecordFailureAsync(
                candidate: candidate,
                failureCode: exception.Code,
                isRetryable: exception.IsRetryable,
                cancellationToken: cancellationToken);
            logger.LogWarning(
                exception,
                "Google Play 待消费交易权威查询失败。StoreTransactionId={StoreTransactionId}, AttemptCount={AttemptCount}",
                candidate.StoreTransactionId,
                candidate.ConsumptionAttemptCount + 1);
            return;
        }

        if (purchase.Quantity != 1
            || purchase.RefundableQuantity < 0
            || purchase.RefundableQuantity > purchase.Quantity
            || !string.Equals(candidate.ProductId, purchase.ProductId, StringComparison.Ordinal))
        {
            await RecordFailureAsync(
                candidate: candidate,
                failureCode: "PURCHASE_INVALID",
                isRetryable: false,
                cancellationToken: cancellationToken);
            logger.LogError(
                "Google Play 待消费交易权威快照不合法。StoreTransactionId={StoreTransactionId}",
                candidate.StoreTransactionId);
            return;
        }

        if (purchase.PurchaseState == GooglePlayPurchaseState.Canceled
            || purchase.RefundableQuantity < purchase.Quantity)
        {
            await ApplyVoidedPurchaseAsync(
                candidate: candidate,
                purchase: purchase,
                cancellationToken: cancellationToken);
            return;
        }

        if (purchase.PurchaseState != GooglePlayPurchaseState.Purchased
            || !purchase.PurchaseCompletedAt.HasValue)
        {
            await RecordFailureAsync(
                candidate: candidate,
                failureCode: "GOOGLE_PURCHASE_NOT_YET_FINAL",
                isRetryable: true,
                cancellationToken: cancellationToken);
            return;
        }

        var result = await mediator.Send(new ReconcileGoogleStorePurchaseCommand(
            StoreTransactionId: candidate.StoreTransactionId,
            PurchaseToken: candidate.PurchaseToken,
            Purchase: purchase), cancellationToken);
        if (result.Outcome is ReconcileGoogleStorePurchaseOutcome.RetryableFailed
            or ReconcileGoogleStorePurchaseOutcome.DeterministicFailed)
        {
            logger.LogWarning(
                "Google Play 待消费交易补偿未完成。StoreTransactionId={StoreTransactionId}, Outcome={Outcome}, FailureCode={FailureCode}",
                candidate.StoreTransactionId,
                result.Outcome,
                result.FailureCode);
        }
    }

    private async Task ApplyVoidedPurchaseAsync(
        PendingConsumptionStoreTransactionResult candidate,
        GooglePlayPurchase purchase,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var factType = purchase.PurchaseState == GooglePlayPurchaseState.Canceled
            ? StoreTransactionFactType.Voided
            : StoreTransactionFactType.Refunded;
        var externalEventId = BuildExternalEventId(
            purchaseToken: candidate.PurchaseToken,
            snapshotHash: purchase.SnapshotHash);
        var factKey = BuildFactKey(
            externalEventId: externalEventId,
            purchaseToken: candidate.PurchaseToken,
            factType: factType);
        var recordCommand = new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: null,
            SourceType: StoreTransactionFactSourceType.Reconciliation,
            Store: AppStore.GooglePlay,
            ExternalTransactionId: candidate.PurchaseToken,
            StoreOrderId: purchase.OrderId,
            ExternalEventId: externalEventId,
            Type: factType,
            OccurredAt: now,
            PlatformVersionAt: null,
            FactKey: factKey,
            PayloadHash: SHA256.HashData(Encoding.UTF8.GetBytes(purchase.SnapshotHash)),
            AppliedStoreTransactionId: null);
        await mediator.Send(recordCommand, cancellationToken);
        await mediator.Send(new ReverseGoogleStorePurchaseCommand(
            StoreTransactionId: candidate.StoreTransactionId,
            RefundedAt: now,
            FactKey: Convert.ToHexString(factKey)), cancellationToken);
        await mediator.Send(recordCommand with
        {
            AppliedStoreTransactionId = candidate.StoreTransactionId
        }, cancellationToken);
    }

    private Task RecordFailureAsync(
        PendingConsumptionStoreTransactionResult candidate,
        string failureCode,
        bool isRetryable,
        CancellationToken cancellationToken)
    {
        return mediator.Send(new RecordGoogleStoreConsumptionFailureCommand(
            StoreTransactionId: candidate.StoreTransactionId,
            FailureCode: failureCode,
            IsRetryable: isRetryable), cancellationToken);
    }

    private static string BuildExternalEventId(string purchaseToken, string snapshotHash)
    {
        var purchaseTokenHash = CanonicalEncoding.EncodeBase64Url(
            SHA256.HashData(Encoding.UTF8.GetBytes(purchaseToken)));
        return $"GOOGLE_RECONCILIATION:{purchaseTokenHash}:{snapshotHash}";
    }

    private static byte[] BuildFactKey(
        string externalEventId,
        string purchaseToken,
        StoreTransactionFactType factType)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        CanonicalEncoding.WriteString(payload, AppStore.GooglePlay.ToString());
        CanonicalEncoding.WriteString(payload, StoreTransactionFactSourceType.Reconciliation.ToString());
        CanonicalEncoding.WriteString(payload, externalEventId);
        CanonicalEncoding.WriteString(payload, purchaseToken);
        CanonicalEncoding.WriteString(payload, factType.ToString());
        return SHA256.HashData(payload.ToArray());
    }
}
