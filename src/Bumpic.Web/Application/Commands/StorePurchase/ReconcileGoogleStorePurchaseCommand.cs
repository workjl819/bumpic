using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Web.Clients.Store;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Services.Store;
using StoreTransactionEntity = Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransaction;
using System.Security.Cryptography;
using System.Text;

namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// 根据 Google Play 权威购买快照归属、消费并入账通知发现的交易。
/// </summary>
public record ReconcileGoogleStorePurchaseCommand(
    StoreTransactionId StoreTransactionId,
    string PurchaseToken,
    GooglePlayPurchase Purchase) : ICommand<ReconcileGoogleStorePurchaseResult>;

/// <summary>
/// Google Play 通知购买对账命令处理器。
/// </summary>
public class ReconcileGoogleStorePurchaseCommandHandler(
    IStoreTransactionRepository storeTransactionRepository,
    IStoreProductRepository storeProductRepository,
    IUserAccountRepository userAccountRepository,
    IGooglePlayClient googlePlayClient,
    IClock clock) : ICommandHandler<ReconcileGoogleStorePurchaseCommand, ReconcileGoogleStorePurchaseResult>
{
    /// <summary>
    /// 仅在权威账户标识可归属时认领交易，随后冻结点数快照、消费并完成入账。
    /// </summary>
    public async Task<ReconcileGoogleStorePurchaseResult> Handle(
        ReconcileGoogleStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await storeTransactionRepository.GetAsync(
                              request.StoreTransactionId,
                              cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        EnsureTransactionMatchesRequest(transaction: transaction, request: request);

        if (IsFinal(status: transaction.Status))
        {
            return Completed(transaction: transaction);
        }

        if (transaction.UserAccountId is null)
        {
            var userAccount = await ResolveAuthoritativeUserAsync(
                authoritativeAccountToken: request.Purchase.ObfuscatedExternalAccountId,
                cancellationToken: cancellationToken);
            if (userAccount is null)
            {
                return new ReconcileGoogleStorePurchaseResult(
                    Outcome: ReconcileGoogleStorePurchaseOutcome.Unlinked,
                    Status: transaction.Status,
                    FailureCode: "PURCHASE_ACCOUNT_UNLINKED");
            }

            transaction.LinkTo(userAccountId: userAccount.Id, now: clock.UtcNow);
        }
        else if (transaction.OwnershipStatus != StoreTransactionOwnershipStatus.Linked)
        {
            return new ReconcileGoogleStorePurchaseResult(
                Outcome: ReconcileGoogleStorePurchaseOutcome.DeterministicFailed,
                Status: transaction.Status,
                FailureCode: "PURCHASE_ACCOUNT_MISMATCH");
        }

        if (transaction.Status == StoreTransactionStatus.PendingVerification)
        {
            var product = await storeProductRepository.FindByProductIdAsync(
                store: AppStore.GooglePlay,
                productId: request.Purchase.ProductId,
                cancellationToken: cancellationToken);
            if (product is null)
            {
                return new ReconcileGoogleStorePurchaseResult(
                    Outcome: ReconcileGoogleStorePurchaseOutcome.RetryableFailed,
                    Status: transaction.Status,
                    FailureCode: "STORE_PRODUCT_NOT_FOUND");
            }

            transaction.CapturePurchase(
                storeProductId: product.Id,
                productId: request.Purchase.ProductId,
                pointsSnapshot: product.Points,
                purchasedAt: request.Purchase.PurchaseCompletedAt!.Value,
                verificationPayloadHash: request.Purchase.SnapshotHash,
                amount: null,
                currencyCode: null,
                platformVersionAt: null,
                now: clock.UtcNow);
            transaction.MarkPendingConsumption(now: clock.UtcNow);
        }

        if (!string.Equals(
                transaction.ProductId,
                request.Purchase.ProductId,
                StringComparison.Ordinal))
        {
            return new ReconcileGoogleStorePurchaseResult(
                Outcome: ReconcileGoogleStorePurchaseOutcome.DeterministicFailed,
                Status: transaction.Status,
                FailureCode: "GOOGLE_PRODUCT_MISMATCH");
        }

        if (request.Purchase.IsConsumed)
        {
            transaction.CompleteVerification(now: clock.UtcNow);
            return Completed(transaction: transaction);
        }

        try
        {
            await googlePlayClient.ConsumeAsync(new GooglePlayConsumptionRequest(
                ProductId: request.Purchase.ProductId,
                PurchaseToken: request.PurchaseToken), cancellationToken);
            transaction.CompleteVerification(now: clock.UtcNow);
            return Completed(transaction: transaction);
        }
        catch (StoreClientException exception)
        {
            var now = clock.UtcNow;
            transaction.RecordConsumptionFailure(
                failureCode: exception.Code,
                nextRetryAt: now.Add(StoreConsumptionRetryPolicy.CalculateDelay(
                    completedAttemptCount: transaction.ConsumptionAttemptCount,
                    isRetryable: exception.IsRetryable)),
                now: now);
            return new ReconcileGoogleStorePurchaseResult(
                Outcome: exception.IsRetryable
                    ? ReconcileGoogleStorePurchaseOutcome.RetryableFailed
                    : ReconcileGoogleStorePurchaseOutcome.DeterministicFailed,
                Status: transaction.Status,
                FailureCode: exception.Code);
        }
    }

    private async Task<UserAccount?> ResolveAuthoritativeUserAsync(
        string? authoritativeAccountToken,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(authoritativeAccountToken, out var purchaseAccountToken))
        {
            return null;
        }

        var userAccount = await userAccountRepository.FindByPurchaseAccountTokenAsync(
            purchaseAccountToken: purchaseAccountToken,
            cancellationToken: cancellationToken);
        return userAccount is not null && !userAccount.Deleted ? userAccount : null;
    }

    private static void EnsureTransactionMatchesRequest(
        StoreTransactionEntity transaction,
        ReconcileGoogleStorePurchaseCommand request)
    {
        if (transaction.Store != AppStore.GooglePlay
            || !string.Equals(
                transaction.ExternalTransactionId,
                request.PurchaseToken,
                StringComparison.Ordinal))
        {
            throw new KnownException("PURCHASE_INVALID");
        }
    }

    private static bool IsFinal(StoreTransactionStatus status)
    {
        return status is StoreTransactionStatus.Verified
            or StoreTransactionStatus.Rejected
            or StoreTransactionStatus.Canceled
            or StoreTransactionStatus.Voided
            or StoreTransactionStatus.Refunded;
    }

    private static ReconcileGoogleStorePurchaseResult Completed(StoreTransactionEntity transaction)
    {
        return new ReconcileGoogleStorePurchaseResult(
            Outcome: ReconcileGoogleStorePurchaseOutcome.Completed,
            Status: transaction.Status,
            FailureCode: transaction.FailureCode);
    }
}

/// <summary>
/// Google Play 通知购买对账命令锁。
/// </summary>
public class ReconcileGoogleStorePurchaseCommandLock
    : ICommandLock<ReconcileGoogleStorePurchaseCommand>
{
    /// <summary>
    /// 与客户端验单共用平台交易锁，避免并发认领或重复消费。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReconcileGoogleStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var identity = $"{AppStore.GooglePlay}:{command.PurchaseToken}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-verification:{digest}",
            acquireSeconds: 30));
    }
}

/// <summary>
/// Google Play 通知购买对账命令验证器。
/// </summary>
public class ReconcileGoogleStorePurchaseCommandValidator
    : AbstractValidator<ReconcileGoogleStorePurchaseCommand>
{
    public ReconcileGoogleStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId.Id).NotEmpty();
        RuleFor(x => x.PurchaseToken).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Purchase).NotNull();
        When(x => x.Purchase is not null, () =>
        {
            RuleFor(x => x.Purchase.ProductId).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Purchase.PurchaseState).Equal(GooglePlayPurchaseState.Purchased);
            RuleFor(x => x.Purchase.Quantity).Equal(1);
            RuleFor(x => x.Purchase.RefundableQuantity).Equal(1);
            RuleFor(x => x.Purchase.PurchaseCompletedAt).NotNull();
            RuleFor(x => x.Purchase.SnapshotHash).NotEmpty().MaximumLength(255);
        });
    }
}
