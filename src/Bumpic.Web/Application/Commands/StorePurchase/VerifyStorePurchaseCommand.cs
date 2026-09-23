using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Application.Commands.IdempotentRequest;
using Bumpic.Web.Application.Commands.StoreTransactionFact;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Services.Store;
using Bumpic.Web.Utils;
using PointAccountEntity = Bumpic.Domain.AggregateModel.PointAccountAggregate.PointAccount;
using StoreTransactionEntity = Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransaction;
using StoreTransactionStatusValue = Bumpic.Domain.Enums.StoreTransactionStatus;

namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// 验证商店购买并完成消费和入账命令。
/// </summary>
public record VerifyStorePurchaseCommand(
    UserAccountId UserAccountId,
    AppStore Store,
    string ProductId,
    string? TransactionId,
    string? TransactionJws,
    string? PurchaseToken,
    string? ExternalAccountToken,
    string IdempotencyKey) : ICommand<VerifyStorePurchaseCommandResult>;

/// <summary>
/// 商店验单命令处理器。
/// </summary>
public class VerifyStorePurchaseCommandHandler(
    IMediator mediator,
    IStoreTransactionRepository storeTransactionRepository,
    IStoreProductRepository storeProductRepository,
    IUserAccountRepository userAccountRepository,
    IPointAccountRepository pointAccountRepository,
    IStoreVerificationRequestHasher requestHasher,
    IAppleStoreClient appleStoreClient,
    IGooglePlayClient googlePlayClient,
    IClock clock) : ICommandHandler<VerifyStorePurchaseCommand, VerifyStorePurchaseCommandResult>
{
    /// <summary>
    /// 验单幂等操作名称。
    /// </summary>
    public const string OperationName = "VerifyStorePurchase";

    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 按权威验单结果消费、入账或返回当前交易状态。
    /// </summary>
    public async Task<VerifyStorePurchaseCommandResult> Handle(
        VerifyStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var externalTransactionId = request.Store == AppStore.AppleAppStore
            ? request.TransactionId!
            : request.PurchaseToken!;
        var evidence = request.Store == AppStore.AppleAppStore
            ? request.TransactionJws!
            : request.PurchaseToken!;
        var requestHash = requestHasher.ComputeRequestHash(
            store: request.Store,
            productId: request.ProductId,
            externalTransactionId: externalTransactionId,
            evidenceHash: requestHasher.ComputeEvidenceHash(evidence: evidence),
            externalAccountToken: request.ExternalAccountToken);

        var acquireResult = await mediator.Send(
            new AcquireIdempotentRequestCommand(
                UserAccountId: request.UserAccountId,
                Operation: OperationName,
                IdempotencyKey: request.IdempotencyKey,
                RequestHash: requestHash,
                RequestHashVersion: requestHasher.Version,
                LeaseDuration: LeaseDuration),
            cancellationToken);
        switch (acquireResult.Result)
        {
            case IdempotentRequestAcquireResult.RequestHashMismatch:
                return DeterministicFailure(failureCode: "IDEMPOTENCY_KEY_REUSED");
            case IdempotentRequestAcquireResult.Processing:
                return Processing(acquireResult: acquireResult, now: now);
            case IdempotentRequestAcquireResult.RetryNotDue:
                return RetryableFailure(
                    failureCode: "STORE_SERVICE_UNAVAILABLE",
                    retryAfterSeconds: ToRetryAfterSeconds(
                        nextRetryAt: acquireResult.NextRetryAt
                                     ?? acquireResult.LeaseExpiresAt
                                     ?? now.Add(RetryDelay),
                        now: now));
            case IdempotentRequestAcquireResult.Completed:
                return await ResolveCompletedRequestAsync(
                    acquireResult: acquireResult,
                    request: request,
                    cancellationToken: cancellationToken);
        }

        var executionResult = await ExecuteAsync(
            request: request,
            externalTransactionId: externalTransactionId,
            now: now,
            cancellationToken: cancellationToken);
        var leaseToken = acquireResult.LeaseToken!.Value;
        if (executionResult.Outcome == StorePurchaseVerificationOutcome.RetryableFailed)
        {
            await mediator.Send(
                new MarkIdempotentRequestRetryableFailedCommand(
                    IdempotentRequestId: acquireResult.IdempotentRequestId,
                    LeaseToken: leaseToken,
                    ResultCode: executionResult.FailureCode ?? "STORE_SERVICE_UNAVAILABLE",
                    NextRetryAt: now.Add(RetryDelay)),
                cancellationToken);
            return executionResult;
        }

        await mediator.Send(
            new CompleteIdempotentRequestCommand(
                IdempotentRequestId: acquireResult.IdempotentRequestId,
                LeaseToken: leaseToken,
                ResultCode: executionResult.FailureCode ?? "OK",
                StoreTransactionId: executionResult.StoreTransactionId),
            cancellationToken);
        return executionResult;
    }

    private async Task<VerifyStorePurchaseCommandResult> ExecuteAsync(
        VerifyStorePurchaseCommand request,
        string externalTransactionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            return request.Store == AppStore.AppleAppStore
                ? await VerifyApplePurchaseAsync(
                    request: request,
                    externalTransactionId: externalTransactionId,
                    now: now,
                    cancellationToken: cancellationToken)
                : await VerifyGooglePurchaseAsync(
                    request: request,
                    externalTransactionId: externalTransactionId,
                    now: now,
                    cancellationToken: cancellationToken);
        }
        catch (StoreClientException exception) when (exception.IsRetryable)
        {
            return RetryableFailure(
                failureCode: exception.Code,
                retryAfterSeconds: (int)RetryDelay.TotalSeconds);
        }
        catch (StoreClientException exception)
        {
            return DeterministicFailure(failureCode: exception.Code);
        }
    }

    private async Task<VerifyStorePurchaseCommandResult> VerifyApplePurchaseAsync(
        VerifyStorePurchaseCommand request,
        string externalTransactionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var appleTransaction = await appleStoreClient.VerifyTransactionAsync(
            new AppleStoreVerificationRequest(
                TransactionId: request.TransactionId!,
                TransactionJws: request.TransactionJws!),
            cancellationToken);
        if (!string.Equals(appleTransaction.TransactionId, externalTransactionId, StringComparison.Ordinal)
            || appleTransaction.Quantity != 1
            || !string.Equals(appleTransaction.ProductId, request.ProductId, StringComparison.Ordinal))
        {
            return DeterministicFailure(failureCode: "PURCHASE_INVALID");
        }

        if (!await IsPurchaseAccountMatchAsync(
                authoritativeToken: appleTransaction.AppAccountToken,
                requestedToken: request.ExternalAccountToken,
                userAccountId: request.UserAccountId,
                cancellationToken: cancellationToken))
        {
            return DeterministicFailure(failureCode: "PURCHASE_ACCOUNT_MISMATCH");
        }

        var product = await storeProductRepository.FindByProductIdAsync(
            store: request.Store,
            productId: appleTransaction.ProductId,
            cancellationToken: cancellationToken);
        if (product is null)
        {
            return DeterministicFailure(failureCode: "STORE_PRODUCT_NOT_FOUND");
        }

        var transaction = await storeTransactionRepository.FindByExternalTransactionIdAsync(
            store: request.Store,
            externalTransactionId: externalTransactionId,
            cancellationToken: cancellationToken);
        var claimConflict = TryClaimTransaction(
            transaction: transaction,
            userAccountId: request.UserAccountId,
            now: now);
        if (claimConflict is not null)
        {
            return claimConflict;
        }

        if (transaction is null)
        {
            transaction = StoreTransactionEntity.CreateLinkedApple(
                externalTransactionId: appleTransaction.TransactionId,
                userAccountId: request.UserAccountId,
                now: now);
            await storeTransactionRepository.AddAsync(transaction, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(transaction.ProductId)
            && !string.Equals(transaction.ProductId, appleTransaction.ProductId, StringComparison.Ordinal))
        {
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: ResolveFailureCode(transaction: transaction),
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        if (appleTransaction.IsRevoked)
        {
            var refundFact = await RecordApplePurchaseFactAsync(
                appleTransaction: appleTransaction,
                type: StoreTransactionFactType.Refunded,
                occurredAt: appleTransaction.RevocationDate ?? appleTransaction.SignedDate,
                cancellationToken: cancellationToken);
            var reversedNow = transaction.ApplyAppleRefund(
                refundedAt: appleTransaction.RevocationDate ?? appleTransaction.SignedDate,
                platformVersionAt: appleTransaction.SignedDate,
                factKey: appleTransaction.PayloadHash,
                now: now);
            await mediator.Send(refundFact with
            {
                AppliedStoreTransactionId = transaction.Id
            }, cancellationToken);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: "PURCHASE_REFUNDED",
                grantedNow: false,
                reversedNow: reversedNow,
                cancellationToken: cancellationToken);
        }

        var purchaseFact = await RecordApplePurchaseFactAsync(
            appleTransaction: appleTransaction,
            type: StoreTransactionFactType.Purchased,
            occurredAt: appleTransaction.PurchasedAt,
            cancellationToken: cancellationToken);

        if (transaction.HasMergedPlatformFactAtOrAfter(platformVersionAt: appleTransaction.SignedDate))
        {
            await mediator.Send(purchaseFact with
            {
                AppliedStoreTransactionId = transaction.Id
            }, cancellationToken);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: ResolveFailureCode(transaction: transaction),
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        if (transaction.Status != StoreTransactionStatusValue.PendingVerification)
        {
            await mediator.Send(purchaseFact with
            {
                AppliedStoreTransactionId = transaction.Id
            }, cancellationToken);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: ResolveFailureCode(transaction: transaction),
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        var captured = transaction.CapturePurchase(
            storeProductId: product.Id,
            productId: appleTransaction.ProductId,
            pointsSnapshot: product.Points,
            purchasedAt: appleTransaction.PurchasedAt,
            verificationPayloadHash: appleTransaction.PayloadHash,
            amount: appleTransaction.Amount,
            currencyCode: appleTransaction.CurrencyCode,
            platformVersionAt: appleTransaction.SignedDate,
            now: now);
        if (!captured)
        {
            await mediator.Send(purchaseFact with
            {
                AppliedStoreTransactionId = transaction.Id
            }, cancellationToken);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: ResolveFailureCode(transaction: transaction),
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        transaction.CompleteVerification(now: now);
        await mediator.Send(purchaseFact with
        {
            AppliedStoreTransactionId = transaction.Id
        }, cancellationToken);
        return await BuildCurrentStateResultAsync(
            transaction: transaction,
            failureCode: null,
            grantedNow: true,
            cancellationToken: cancellationToken);
    }

    private async Task<RecordStoreTransactionFactCommand> RecordApplePurchaseFactAsync(
        AppleStoreTransaction appleTransaction,
        StoreTransactionFactType type,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var externalEventId =
            $"APPLE_SNAPSHOT:{appleTransaction.TransactionId}:{appleTransaction.SignedDate.ToUniversalTime().ToUnixTimeMilliseconds()}";
        var factCommand = new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: null,
            SourceType: StoreTransactionFactSourceType.ClientVerification,
            Store: AppStore.AppleAppStore,
            ExternalTransactionId: appleTransaction.TransactionId,
            StoreOrderId: null,
            ExternalEventId: externalEventId,
            Type: type,
            OccurredAt: occurredAt,
            PlatformVersionAt: appleTransaction.SignedDate,
            FactKey: BuildAppleFactKey(
                externalEventId: externalEventId,
                externalTransactionId: appleTransaction.TransactionId,
                type: type),
            PayloadHash: SHA256.HashData(Encoding.UTF8.GetBytes(appleTransaction.PayloadHash)),
            AppliedStoreTransactionId: null);
        await mediator.Send(factCommand, cancellationToken);
        return factCommand;
    }

    private async Task<VerifyStorePurchaseCommandResult> VerifyGooglePurchaseAsync(
        VerifyStorePurchaseCommand request,
        string externalTransactionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var purchase = await googlePlayClient.GetPurchaseAsync(
            new GooglePlayPurchaseRequest(
                PurchaseToken: request.PurchaseToken!),
            cancellationToken);
        if (purchase.Quantity != 1
            || !string.Equals(purchase.ProductId, request.ProductId, StringComparison.Ordinal))
        {
            return DeterministicFailure(failureCode: "PURCHASE_INVALID");
        }

        if (!string.IsNullOrWhiteSpace(request.TransactionId)
            && !string.Equals(request.TransactionId, purchase.OrderId, StringComparison.Ordinal))
        {
            return DeterministicFailure(failureCode: "GOOGLE_ORDER_ID_MISMATCH");
        }

        if (!await IsPurchaseAccountMatchAsync(
                authoritativeToken: purchase.ObfuscatedExternalAccountId,
                requestedToken: request.ExternalAccountToken,
                userAccountId: request.UserAccountId,
                cancellationToken: cancellationToken))
        {
            return DeterministicFailure(failureCode: "PURCHASE_ACCOUNT_MISMATCH");
        }

        var transaction = await storeTransactionRepository.FindByExternalTransactionIdAsync(
            store: request.Store,
            externalTransactionId: externalTransactionId,
            cancellationToken: cancellationToken);
        var claimConflict = TryClaimTransaction(
            transaction: transaction,
            userAccountId: request.UserAccountId,
            now: now);
        if (claimConflict is not null)
        {
            return claimConflict;
        }

        if (purchase.PurchaseState == GooglePlayPurchaseState.Canceled)
        {
            if (transaction is null)
            {
                transaction = await AddLinkedGoogleTransactionAsync(
                    request: request,
                    externalTransactionId: externalTransactionId,
                    purchase: purchase,
                    now: now,
                    cancellationToken: cancellationToken);
            }

            if (transaction.Status == StoreTransactionStatusValue.PendingVerification)
            {
                transaction.MarkCanceled(
                    now: now);
            }

            await RecordGooglePurchaseFactAsync(
                transaction: transaction,
                externalTransactionId: externalTransactionId,
                purchase: purchase,
                type: StoreTransactionFactType.Canceled,
                now: now,
                cancellationToken: cancellationToken);

            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: "PURCHASE_CANCELED",
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        var product = await storeProductRepository.FindByProductIdAsync(
            store: request.Store,
            productId: purchase.ProductId,
            cancellationToken: cancellationToken);
        if (product is null)
        {
            return DeterministicFailure(failureCode: "STORE_PRODUCT_NOT_FOUND");
        }

        var purchasedAt = purchase.PurchaseCompletedAt;
        if (transaction is null)
        {
            transaction = await AddLinkedGoogleTransactionAsync(
                request: request,
                externalTransactionId: externalTransactionId,
                purchase: purchase,
                now: now,
                cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(transaction.ProductId)
            && !string.Equals(transaction.ProductId, purchase.ProductId, StringComparison.Ordinal))
        {
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: ResolveFailureCode(transaction: transaction),
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        if (purchase.RefundableQuantity < purchase.Quantity)
        {
            var reversedNow = transaction.ApplyGoogleFullRefund(
                refundedAt: purchase.PurchaseCompletedAt ?? now,
                factKey: purchase.SnapshotHash,
                now: now);
            await RecordGooglePurchaseFactAsync(
                transaction: transaction,
                externalTransactionId: externalTransactionId,
                purchase: purchase,
                type: StoreTransactionFactType.Refunded,
                now: now,
                cancellationToken: cancellationToken);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: "PURCHASE_REFUNDED",
                grantedNow: false,
                reversedNow: reversedNow,
                cancellationToken: cancellationToken);
        }

        if (purchase.PurchaseState == GooglePlayPurchaseState.Purchased)
        {
            await RecordGooglePurchaseFactAsync(
                transaction: transaction,
                externalTransactionId: externalTransactionId,
                purchase: purchase,
                type: StoreTransactionFactType.Purchased,
                now: now,
                cancellationToken: cancellationToken);
        }

        if (transaction.Status is StoreTransactionStatusValue.Canceled
            or StoreTransactionStatusValue.Voided
            or StoreTransactionStatusValue.Rejected
            or StoreTransactionStatusValue.Verified
            or StoreTransactionStatusValue.Refunded)
        {
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: ResolveFailureCode(transaction: transaction),
                grantedNow: false,
                cancellationToken: cancellationToken);
        }

        if (transaction.Status == StoreTransactionStatusValue.PendingVerification)
        {
            if (purchase.PurchaseState == GooglePlayPurchaseState.Pending || !purchasedAt.HasValue)
            {
                return await BuildCurrentStateResultAsync(
                    transaction: transaction,
                    failureCode: "PURCHASE_PENDING",
                    grantedNow: false,
                    cancellationToken: cancellationToken);
            }

            transaction.CapturePurchase(
                storeProductId: product.Id,
                productId: purchase.ProductId,
                pointsSnapshot: product.Points,
                purchasedAt: purchasedAt.Value,
                verificationPayloadHash: purchase.SnapshotHash,
                amount: null,
                currencyCode: null,
                platformVersionAt: null,
                now: now);
            transaction.MarkPendingConsumption(now: now);
        }

        if (purchase.IsConsumed)
        {
            transaction.CompleteVerification(now: now);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: null,
                grantedNow: true,
                cancellationToken: cancellationToken);
        }

        try
        {
            await googlePlayClient.ConsumeAsync(
                new GooglePlayConsumptionRequest(
                    ProductId: purchase.ProductId,
                    PurchaseToken: request.PurchaseToken!),
                cancellationToken);
            transaction.CompleteVerification(now: now);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: null,
                grantedNow: true,
                cancellationToken: cancellationToken);
        }
        catch (StoreClientException exception)
        {
            transaction.RecordConsumptionFailure(
                failureCode: exception.Code,
                nextRetryAt: now.Add(RetryDelay),
                now: now);
            return await BuildCurrentStateResultAsync(
                transaction: transaction,
                failureCode: exception.Code,
                grantedNow: false,
                cancellationToken: cancellationToken);
        }
    }

    private async Task<StoreTransactionEntity> AddLinkedGoogleTransactionAsync(
        VerifyStorePurchaseCommand request,
        string externalTransactionId,
        GooglePlayPurchase purchase,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var transaction = StoreTransactionEntity.CreateLinkedGoogle(
            externalTransactionId: externalTransactionId,
            userAccountId: request.UserAccountId,
            isTestPurchase: purchase.IsTestPurchase,
            now: now);
        await storeTransactionRepository.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    private async Task RecordGooglePurchaseFactAsync(
        StoreTransactionEntity transaction,
        string externalTransactionId,
        GooglePlayPurchase purchase,
        StoreTransactionFactType type,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var purchaseTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(externalTransactionId));
        var purchaseTokenHashText = CanonicalEncoding.EncodeBase64Url(purchaseTokenHash);
        var externalEventId = $"GOOGLE_SNAPSHOT:{purchaseTokenHashText}:{purchase.SnapshotHash}";
        var factKey = BuildGoogleFactKey(
            externalEventId: externalEventId,
            externalTransactionId: externalTransactionId,
            type: type);
        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(purchase.SnapshotHash));
        await mediator.Send(new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: null,
            SourceType: StoreTransactionFactSourceType.ClientVerification,
            Store: AppStore.GooglePlay,
            ExternalTransactionId: externalTransactionId,
            StoreOrderId: purchase.OrderId,
            ExternalEventId: externalEventId,
            Type: type,
            OccurredAt: purchase.PurchaseCompletedAt ?? now,
            PlatformVersionAt: null,
            FactKey: factKey,
            PayloadHash: payloadHash,
            AppliedStoreTransactionId: transaction.Id), cancellationToken);
    }

    private static byte[] BuildGoogleFactKey(
        string externalEventId,
        string externalTransactionId,
        StoreTransactionFactType type)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        CanonicalEncoding.WriteString(payload, AppStore.GooglePlay.ToString());
        CanonicalEncoding.WriteString(payload, StoreTransactionFactSourceType.ClientVerification.ToString());
        CanonicalEncoding.WriteString(payload, externalEventId);
        CanonicalEncoding.WriteString(payload, externalTransactionId);
        CanonicalEncoding.WriteString(payload, type.ToString());
        return SHA256.HashData(payload.ToArray());
    }

    private static byte[] BuildAppleFactKey(
        string externalEventId,
        string externalTransactionId,
        StoreTransactionFactType type)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        CanonicalEncoding.WriteString(payload, AppStore.AppleAppStore.ToString());
        CanonicalEncoding.WriteString(payload, StoreTransactionFactSourceType.ClientVerification.ToString());
        CanonicalEncoding.WriteString(payload, externalEventId);
        CanonicalEncoding.WriteString(payload, externalTransactionId);
        CanonicalEncoding.WriteString(payload, type.ToString());
        return SHA256.HashData(payload.ToArray());
    }

    private async Task<VerifyStorePurchaseCommandResult> ResolveCompletedRequestAsync(
        AcquireIdempotentRequestResult acquireResult,
        VerifyStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        if (acquireResult.StoreTransactionId is null)
        {
            return DeterministicFailure(
                failureCode: acquireResult.ResultCode ?? "PURCHASE_INVALID");
        }

        var transaction = await storeTransactionRepository.GetAsync(
            acquireResult.StoreTransactionId,
            cancellationToken);
        if (transaction is null || transaction.UserAccountId != request.UserAccountId)
        {
            return DeterministicFailure(failureCode: "PURCHASE_NOT_FOUND");
        }

        return await BuildCurrentStateResultAsync(
            transaction: transaction,
            failureCode: ResolveFailureCode(transaction: transaction),
            grantedNow: false,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> IsPurchaseAccountMatchAsync(
        string? authoritativeToken,
        string? requestedToken,
        UserAccountId userAccountId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(authoritativeToken, out var authoritativePurchaseAccountToken)
            || !Guid.TryParse(requestedToken, out var requestedPurchaseAccountToken)
            || authoritativePurchaseAccountToken != requestedPurchaseAccountToken)
        {
            return false;
        }

        var userAccount = await userAccountRepository.FindByPurchaseAccountTokenAsync(
            purchaseAccountToken: authoritativePurchaseAccountToken,
            cancellationToken: cancellationToken);
        return userAccount is not null
               && !userAccount.Deleted
               && userAccount.Id == userAccountId;
    }

    private static VerifyStorePurchaseCommandResult? TryClaimTransaction(
        StoreTransactionEntity? transaction,
        UserAccountId userAccountId,
        DateTimeOffset now)
    {
        if (transaction is null)
        {
            return null;
        }

        if (transaction.UserAccountId is null)
        {
            transaction.LinkTo(userAccountId: userAccountId, now: now);
            return null;
        }

        if (transaction.UserAccountId == userAccountId)
        {
            return null;
        }

        return DeterministicFailure(failureCode: "PURCHASE_ALREADY_CLAIMED");
    }

    private async Task<VerifyStorePurchaseCommandResult> BuildCurrentStateResultAsync(
        StoreTransactionEntity transaction,
        string? failureCode,
        bool grantedNow,
        CancellationToken cancellationToken,
        int reversedNow = 0)
    {
        var account = transaction.UserAccountId is null
            ? null
            : await pointAccountRepository.GetByUserAccountIdAsync(
                userAccountId: transaction.UserAccountId,
                cancellationToken: cancellationToken);
        return new VerifyStorePurchaseCommandResult(
            Outcome: StorePurchaseVerificationOutcome.Succeeded,
            FailureCode: failureCode,
            RetryAfterSeconds: 0,
            StoreTransactionId: transaction.Id,
            Status: transaction.Status,
            ProductId: transaction.ProductId,
            GrantedPoints: transaction.GrantedPoints,
            ReversedPoints: transaction.ReversedPoints,
            AvailablePoints: CalculateAvailablePoints(
                account: account,
                transaction: transaction,
                grantedNow: grantedNow,
                reversedNow: reversedNow),
            FrozenPoints: account?.FrozenPoints ?? 0);
    }

    private static int CalculateAvailablePoints(
        PointAccountEntity? account,
        StoreTransactionEntity transaction,
        bool grantedNow,
        int reversedNow)
    {
        var currentPoints = account?.AvailablePoints ?? 0;
        var grantedPoints = grantedNow ? transaction.GrantedPoints : 0;
        return currentPoints + grantedPoints - reversedNow;
    }

    private static string? ResolveFailureCode(StoreTransactionEntity transaction)
    {
        return transaction.Status switch
        {
            StoreTransactionStatusValue.Verified => null,
            StoreTransactionStatusValue.PendingVerification => "PURCHASE_PENDING",
            StoreTransactionStatusValue.PendingConsumption => "PURCHASE_CONSUMPTION_PENDING",
            StoreTransactionStatusValue.Refunded or StoreTransactionStatusValue.Voided => "PURCHASE_REFUNDED",
            StoreTransactionStatusValue.Canceled => "PURCHASE_CANCELED",
            _ => transaction.FailureCode
        };
    }

    private static int ToRetryAfterSeconds(DateTimeOffset nextRetryAt, DateTimeOffset now)
    {
        var seconds = (int)Math.Ceiling((nextRetryAt - now).TotalSeconds);
        return Math.Clamp(seconds, 1, 600);
    }

    private static VerifyStorePurchaseCommandResult Processing(
        AcquireIdempotentRequestResult acquireResult,
        DateTimeOffset now)
    {
        return new VerifyStorePurchaseCommandResult(
            Outcome: StorePurchaseVerificationOutcome.Processing,
            FailureCode: "IDEMPOTENCY_REQUEST_PROCESSING",
            RetryAfterSeconds: ToRetryAfterSeconds(
                nextRetryAt: acquireResult.LeaseExpiresAt ?? now.AddSeconds(2),
                now: now),
            StoreTransactionId: acquireResult.StoreTransactionId,
            Status: null,
            ProductId: null,
            GrantedPoints: 0,
            ReversedPoints: 0,
            AvailablePoints: 0,
            FrozenPoints: 0);
    }

    private static VerifyStorePurchaseCommandResult RetryableFailure(
        string failureCode,
        int retryAfterSeconds)
    {
        return new VerifyStorePurchaseCommandResult(
            Outcome: StorePurchaseVerificationOutcome.RetryableFailed,
            FailureCode: failureCode,
            RetryAfterSeconds: retryAfterSeconds,
            StoreTransactionId: null,
            Status: null,
            ProductId: null,
            GrantedPoints: 0,
            ReversedPoints: 0,
            AvailablePoints: 0,
            FrozenPoints: 0);
    }

    private static VerifyStorePurchaseCommandResult DeterministicFailure(string failureCode)
    {
        return new VerifyStorePurchaseCommandResult(
            Outcome: StorePurchaseVerificationOutcome.DeterministicFailed,
            FailureCode: failureCode,
            RetryAfterSeconds: 0,
            StoreTransactionId: null,
            Status: null,
            ProductId: null,
            GrantedPoints: 0,
            ReversedPoints: 0,
            AvailablePoints: 0,
            FrozenPoints: 0);
    }
}

/// <summary>
/// 商店验单命令锁。
/// </summary>
public class VerifyStorePurchaseCommandLock : ICommandLock<VerifyStorePurchaseCommand>
{
    /// <summary>
    /// 同一平台交易的验单和认领过程串行执行，锁键不包含平台凭据明文。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        VerifyStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var externalTransactionId = command.Store == AppStore.AppleAppStore
            ? command.TransactionId
            : command.PurchaseToken;
        var identity = $"{command.Store}:{externalTransactionId}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-verification:{digest}",
            acquireSeconds: 30));
    }
}

/// <summary>
/// 商店验单命令验证器。
/// </summary>
public class VerifyStorePurchaseCommandValidator : AbstractValidator<VerifyStorePurchaseCommand>
{
    /// <summary>
    /// 初始化验单请求字段规则。
    /// </summary>
    public VerifyStorePurchaseCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull();
        RuleFor(x => x.Store).IsInEnum();
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(255);

        When(x => x.Store == AppStore.AppleAppStore, () =>
        {
            RuleFor(x => x.TransactionId).NotEmpty().MaximumLength(255);
            RuleFor(x => x.TransactionJws).NotEmpty();
            RuleFor(x => x.PurchaseToken).Empty();
        });

        When(x => x.Store == AppStore.GooglePlay, () =>
        {
            RuleFor(x => x.PurchaseToken).NotEmpty();
            RuleFor(x => x.TransactionId).MaximumLength(255);
            RuleFor(x => x.TransactionJws).Empty();
        });
    }
}
