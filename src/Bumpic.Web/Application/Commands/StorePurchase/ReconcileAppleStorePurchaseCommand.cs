using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Web.Clients.Store;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using StoreTransactionEntity = Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransaction;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;

namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// 根据 Apple 服务端通知的权威交易快照归属并入账。
/// </summary>
public record ReconcileAppleStorePurchaseCommand(
    StoreTransactionId StoreTransactionId,
    AppleStoreTransaction Transaction) : ICommand<ReconcileAppleStorePurchaseResult>;

/// <summary>
/// Apple 通知购买对账命令处理器。
/// </summary>
public class ReconcileAppleStorePurchaseCommandHandler(
    IStoreTransactionRepository storeTransactionRepository,
    IStoreProductRepository storeProductRepository,
    IUserAccountRepository userAccountRepository,
    IClock clock) : ICommandHandler<ReconcileAppleStorePurchaseCommand, ReconcileAppleStorePurchaseResult>
{
    /// <summary>
    /// 仅在 Apple 权威账户标识匹配后认领交易并完成点数入账。
    /// </summary>
    public async Task<ReconcileAppleStorePurchaseResult> Handle(
        ReconcileAppleStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await storeTransactionRepository.GetAsync(
                              request.StoreTransactionId,
                              cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        EnsureTransactionMatchesRequest(transaction, request);

        if (request.Transaction.Quantity != 1
            || !string.Equals(request.Transaction.TransactionType, "Consumable", StringComparison.Ordinal)
            || request.Transaction.IsRevoked)
        {
            return new ReconcileAppleStorePurchaseResult(
                ReconcileAppleStorePurchaseOutcome.DeterministicFailed,
                transaction.Status,
                "PURCHASE_INVALID");
        }

        if (IsFinal(transaction.Status))
        {
            return Completed(transaction);
        }

        if (transaction.UserAccountId is null)
        {
            var userAccount = await ResolveAuthoritativeUserAsync(
                request.Transaction.AppAccountToken,
                cancellationToken);
            if (userAccount is null)
            {
                return new ReconcileAppleStorePurchaseResult(
                    ReconcileAppleStorePurchaseOutcome.Unlinked,
                    transaction.Status,
                    "PURCHASE_ACCOUNT_UNLINKED");
            }

            transaction.LinkTo(userAccount.Id, clock.UtcNow);
        }
        else if (transaction.OwnershipStatus != StoreTransactionOwnershipStatus.Linked)
        {
            return new ReconcileAppleStorePurchaseResult(
                ReconcileAppleStorePurchaseOutcome.DeterministicFailed,
                transaction.Status,
                "PURCHASE_ACCOUNT_MISMATCH");
        }

        if (!string.Equals(transaction.ProductId, request.Transaction.ProductId, StringComparison.Ordinal))
        {
            if (transaction.ProductId is not null)
            {
                return new ReconcileAppleStorePurchaseResult(
                    ReconcileAppleStorePurchaseOutcome.DeterministicFailed,
                    transaction.Status,
                    "APPLE_PRODUCT_MISMATCH");
            }
        }

        if (transaction.Status == StoreTransactionStatus.PendingVerification)
        {
            var product = await storeProductRepository.FindByProductIdAsync(
                AppStore.AppleAppStore,
                request.Transaction.ProductId,
                cancellationToken);
            if (product is null)
            {
                return new ReconcileAppleStorePurchaseResult(
                    ReconcileAppleStorePurchaseOutcome.RetryableFailed,
                    transaction.Status,
                    "STORE_PRODUCT_NOT_FOUND");
            }

            transaction.CapturePurchase(
                storeProductId: product.Id,
                productId: request.Transaction.ProductId,
                pointsSnapshot: product.Points,
                purchasedAt: request.Transaction.PurchasedAt,
                verificationPayloadHash: request.Transaction.PayloadHash,
                amount: request.Transaction.Amount,
                currencyCode: request.Transaction.CurrencyCode,
                platformVersionAt: request.Transaction.SignedDate,
                now: clock.UtcNow);
            transaction.CompleteVerification(clock.UtcNow);
        }

        return Completed(transaction);
    }

    private async Task<UserAccount?> ResolveAuthoritativeUserAsync(
        string? appAccountToken,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(appAccountToken, out var token))
        {
            return null;
        }

        var account = await userAccountRepository.FindByPurchaseAccountTokenAsync(token, cancellationToken);
        return account is not null && !account.Deleted ? account : null;
    }

    private static void EnsureTransactionMatchesRequest(
        StoreTransactionEntity transaction,
        ReconcileAppleStorePurchaseCommand request)
    {
        if (transaction.Store != AppStore.AppleAppStore
            || !string.Equals(
                transaction.ExternalTransactionId,
                request.Transaction.TransactionId,
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

    private static ReconcileAppleStorePurchaseResult Completed(StoreTransactionEntity transaction)
    {
        return new ReconcileAppleStorePurchaseResult(
            ReconcileAppleStorePurchaseOutcome.Completed,
            transaction.Status,
            transaction.FailureCode);
    }
}

/// <summary>
/// Apple 服务端购买对账命令锁。
/// </summary>
public class ReconcileAppleStorePurchaseCommandLock
    : ICommandLock<ReconcileAppleStorePurchaseCommand>
{
    /// <summary>
    /// 与客户端验单和通知导入共用平台交易锁。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReconcileAppleStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var identity = $"{AppStore.AppleAppStore}:{command.Transaction.TransactionId}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-verification:{digest}",
            acquireSeconds: 30));
    }
}

/// <summary>
/// Apple 服务端购买补偿命令校验器。
/// </summary>
public class ReconcileAppleStorePurchaseCommandValidator
    : AbstractValidator<ReconcileAppleStorePurchaseCommand>
{
    /// <summary>
    /// 初始化校验规则。
    /// </summary>
    public ReconcileAppleStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId.Id).NotEmpty();
        RuleFor(x => x.Transaction).NotNull();
    }
}
