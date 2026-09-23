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
/// 每小时补查仍处于待验证状态的 Apple 和 Google 交易。
/// </summary>
public class VerifyPendingStoreTransactionJob(
    IMediator mediator,
    IAppleAppStoreServerApiClient appleClient,
    IGooglePlayClient googleClient,
    IClock clock,
    ILogger<VerifyPendingStoreTransactionJob> logger)
{
    private static readonly TimeSpan AlertThreshold = TimeSpan.FromHours(24);

    /// <summary>
    /// 查询平台权威状态并复用现有命令完成购买、取消或退款状态合并。
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var candidates = await mediator.Send(
            new GetPendingVerificationStoreTransactionsQuery(),
            cancellationToken);
        foreach (var candidate in candidates)
        {
            await ProcessOneAsync(candidate, cancellationToken);
        }
    }

    private async Task ProcessOneAsync(
        PendingVerificationStoreTransactionResult candidate,
        CancellationToken cancellationToken)
    {
        if (clock.UtcNow - candidate.CreatedAt >= AlertThreshold)
        {
            logger.LogError(
                "商店交易等待验证已超过 24 小时。StoreTransactionId={StoreTransactionId}, Store={Store}, CreatedAt={CreatedAt}",
                candidate.StoreTransactionId,
                candidate.Store,
                candidate.CreatedAt);
        }

        try
        {
            if (candidate.Store == AppStore.AppleAppStore)
            {
                await ProcessAppleAsync(candidate, cancellationToken);
                return;
            }

            await ProcessGoogleAsync(candidate, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (StoreClientException exception)
        {
            logger.LogWarning(
                exception,
                "商店待验证交易权威查询失败，保留 PendingVerification 等待下次补查。StoreTransactionId={StoreTransactionId}, Store={Store}, Code={Code}",
                candidate.StoreTransactionId,
                candidate.Store,
                exception.Code);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "商店待验证交易补查失败，保留 PendingVerification。StoreTransactionId={StoreTransactionId}, Store={Store}",
                candidate.StoreTransactionId,
                candidate.Store);
        }
    }

    private async Task ProcessAppleAsync(
        PendingVerificationStoreTransactionResult candidate,
        CancellationToken cancellationToken)
    {
        var signedPayload = await appleClient.GetTransactionInfoAsync(
            candidate.ExternalTransactionId,
            cancellationToken);
        var transactionPayload = AppleTransactionPayload.Read(signedPayload.Payload);
        var transaction = transactionPayload.CreateSnapshot(signedPayload.PayloadHash);
        if (!string.Equals(
                transaction.TransactionId,
                candidate.ExternalTransactionId,
                StringComparison.Ordinal)
            || transaction.Quantity != 1)
        {
            logger.LogError(
                "Apple 待验证交易权威快照不合法。StoreTransactionId={StoreTransactionId}",
                candidate.StoreTransactionId);
            return;
        }

        if (transaction.IsRevoked)
        {
            await ApplyAppleRefundAsync(candidate, transaction, cancellationToken);
            return;
        }

        var result = await mediator.Send(new ReconcileAppleStorePurchaseCommand(
            candidate.StoreTransactionId,
            transaction), cancellationToken);
        if (result.Outcome != ReconcileAppleStorePurchaseOutcome.Completed)
        {
            logger.LogWarning(
                "Apple 待验证交易本次未完成入账。StoreTransactionId={StoreTransactionId}, Outcome={Outcome}, FailureCode={FailureCode}",
                candidate.StoreTransactionId,
                result.Outcome,
                result.FailureCode);
        }
    }

    private async Task ProcessGoogleAsync(
        PendingVerificationStoreTransactionResult candidate,
        CancellationToken cancellationToken)
    {
        var purchase = await googleClient.GetPurchaseAsync(
            new GooglePlayPurchaseRequest(candidate.ExternalTransactionId),
            cancellationToken);
        if (purchase.Quantity != 1
            || purchase.RefundableQuantity < 0
            || purchase.RefundableQuantity > purchase.Quantity)
        {
            logger.LogError(
                "Google Play 待验证交易权威快照不合法。StoreTransactionId={StoreTransactionId}",
                candidate.StoreTransactionId);
            return;
        }

        if (purchase.PurchaseState == GooglePlayPurchaseState.Canceled)
        {
            await ApplyGoogleTerminalFactAsync(
                candidate,
                purchase,
                StoreTransactionFactType.Canceled,
                cancellationToken);
            return;
        }

        if (purchase.RefundableQuantity < purchase.Quantity)
        {
            await ApplyGoogleTerminalFactAsync(
                candidate,
                purchase,
                StoreTransactionFactType.Refunded,
                cancellationToken);
            return;
        }

        if (purchase.PurchaseState != GooglePlayPurchaseState.Purchased
            || !purchase.PurchaseCompletedAt.HasValue)
        {
            return;
        }

        var result = await mediator.Send(new ReconcileGoogleStorePurchaseCommand(
            candidate.StoreTransactionId,
            candidate.ExternalTransactionId,
            purchase), cancellationToken);
        if (result.Outcome != ReconcileGoogleStorePurchaseOutcome.Completed)
        {
            logger.LogWarning(
                "Google Play 待验证交易本次未完成入账。StoreTransactionId={StoreTransactionId}, Outcome={Outcome}, FailureCode={FailureCode}",
                candidate.StoreTransactionId,
                result.Outcome,
                result.FailureCode);
        }
    }

    private async Task ApplyAppleRefundAsync(
        PendingVerificationStoreTransactionResult candidate,
        AppleStoreTransaction transaction,
        CancellationToken cancellationToken)
    {
        var occurredAt = transaction.RevocationDate ?? transaction.SignedDate;
        var factCommand = BuildFactCommand(
            candidate,
            StoreTransactionFactType.Refunded,
            occurredAt,
            transaction.SignedDate,
            transaction.PayloadHash,
            null);
        await mediator.Send(factCommand, cancellationToken);
        await mediator.Send(new ReverseStorePurchaseCommand(
            candidate.StoreTransactionId,
            occurredAt,
            transaction.SignedDate,
            Convert.ToHexString(factCommand.FactKey)), cancellationToken);
        await mediator.Send(factCommand with
        {
            AppliedStoreTransactionId = candidate.StoreTransactionId
        }, cancellationToken);
    }

    private async Task ApplyGoogleTerminalFactAsync(
        PendingVerificationStoreTransactionResult candidate,
        GooglePlayPurchase purchase,
        StoreTransactionFactType factType,
        CancellationToken cancellationToken)
    {
        var occurredAt = new DateTimeOffset(clock.UtcNow, TimeSpan.Zero);
        var factCommand = BuildFactCommand(
            candidate,
            factType,
            occurredAt,
            null,
            purchase.SnapshotHash,
            purchase.OrderId);
        await mediator.Send(factCommand, cancellationToken);
        if (factType == StoreTransactionFactType.Canceled)
        {
            await mediator.Send(
                new CancelGoogleStorePurchaseCommand(candidate.StoreTransactionId),
                cancellationToken);
        }
        else
        {
            await mediator.Send(new ReverseGoogleStorePurchaseCommand(
                candidate.StoreTransactionId,
                occurredAt,
                Convert.ToHexString(factCommand.FactKey)), cancellationToken);
        }

        await mediator.Send(factCommand with
        {
            AppliedStoreTransactionId = candidate.StoreTransactionId
        }, cancellationToken);
    }

    private static RecordStoreTransactionFactCommand BuildFactCommand(
        PendingVerificationStoreTransactionResult candidate,
        StoreTransactionFactType factType,
        DateTimeOffset occurredAt,
        DateTimeOffset? platformVersionAt,
        string snapshotHash,
        string? storeOrderId)
    {
        var externalEventId = BuildExternalEventId(candidate, snapshotHash);
        var factKey = BuildFactKey(candidate, factType, externalEventId);
        return new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: null,
            SourceType: StoreTransactionFactSourceType.Reconciliation,
            Store: candidate.Store,
            ExternalTransactionId: candidate.ExternalTransactionId,
            StoreOrderId: storeOrderId,
            ExternalEventId: externalEventId,
            Type: factType,
            OccurredAt: occurredAt,
            PlatformVersionAt: platformVersionAt,
            FactKey: factKey,
            PayloadHash: SHA256.HashData(Encoding.UTF8.GetBytes(snapshotHash)),
            AppliedStoreTransactionId: null);
    }

    private static string BuildExternalEventId(
        PendingVerificationStoreTransactionResult candidate,
        string snapshotHash)
    {
        var identity = $"{candidate.Store}:{candidate.ExternalTransactionId}:{snapshotHash}";
        return $"RECONCILIATION:{CanonicalEncoding.EncodeBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))}";
    }

    private static byte[] BuildFactKey(
        PendingVerificationStoreTransactionResult candidate,
        StoreTransactionFactType factType,
        string externalEventId)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        CanonicalEncoding.WriteString(payload, candidate.Store.ToString());
        CanonicalEncoding.WriteString(payload, StoreTransactionFactSourceType.Reconciliation.ToString());
        CanonicalEncoding.WriteString(payload, externalEventId);
        CanonicalEncoding.WriteString(payload, candidate.ExternalTransactionId);
        CanonicalEncoding.WriteString(payload, factType.ToString());
        return SHA256.HashData(payload.ToArray());
    }
}
