using MediatR;
using Moq;
using NetCorePal.Extensions.Primitives;
using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Application.Commands.IdempotentRequest;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransactionFact;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Services.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 商店验单命令处理器测试。
/// </summary>
public class VerifyStorePurchaseCommandHandlerTests
{
    private const string ProductId = "com.lumavill.photorescue.dev.points100";
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IStoreTransactionRepository> _storeTransactionRepository = new();
    private readonly Mock<IStoreProductRepository> _storeProductRepository = new();
    private readonly Mock<IUserAccountRepository> _userAccountRepository = new();
    private readonly Mock<IPointAccountRepository> _pointAccountRepository = new();
    private readonly Mock<IAppleStoreClient> _appleStoreClient = new();
    private readonly Mock<IGooglePlayClient> _googlePlayClient = new();
    private readonly Mock<IClock> _clock = new();
    private readonly UserAccountId _userAccountId = new(Guid.NewGuid());

    /// <summary>
    /// 初始化默认的时钟、仓库和幂等领取结果。
    /// </summary>
    public VerifyStorePurchaseCommandHandlerTests()
    {
        _clock.SetupGet(x => x.UtcNow).Returns(Now.UtcDateTime);
        _storeTransactionRepository
            .Setup(x => x.AddAsync(It.IsAny<StoreTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoreTransaction transaction, CancellationToken _) => transaction);
    }

    /// <summary>
    /// Apple 有效交易在权威账户归属匹配后入账并返回当前余额。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Grant_Points_For_Valid_Apple_Transaction()
    {
        var appAccountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: appAccountToken);
        SetupProduct(points: 100);
        SetupPointAccount(availablePoints: 15);
        _appleStoreClient
            .Setup(x => x.VerifyTransactionAsync(
                It.IsAny<AppleStoreVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAppleTransaction(appAccountToken: appAccountToken));
        StoreTransaction? addedTransaction = null;
        _storeTransactionRepository
            .Setup(x => x.AddAsync(It.IsAny<StoreTransaction>(), It.IsAny<CancellationToken>()))
            .Callback((StoreTransaction transaction, CancellationToken _) => addedTransaction = transaction)
            .ReturnsAsync((StoreTransaction transaction, CancellationToken _) => transaction);

        var result = await CreateHandler().Handle(
            CreateAppleCommand(appAccountToken: appAccountToken),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.Succeeded, result.Outcome);
        Assert.Equal(StoreTransactionStatus.Verified, result.Status);
        Assert.Equal(100, result.GrantedPoints);
        Assert.Equal(115, result.AvailablePoints);
        Assert.NotNull(addedTransaction);
        Assert.Equal(StoreTransactionStatus.Verified, addedTransaction!.Status);
        Assert.Equal(StoreTransactionOwnershipStatus.Linked, addedTransaction.OwnershipStatus);
        Assert.Equal(_userAccountId, addedTransaction.UserAccountId);
        _mediator.Verify(x => x.Send(
            It.Is<CompleteIdempotentRequestCommand>(command =>
                command.ResultCode == "OK" && command.StoreTransactionId == addedTransaction.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Store == AppStore.AppleAppStore
                && command.ExternalTransactionId == "2000000123456789"
                && command.SourceType == StoreTransactionFactSourceType.ClientVerification
                && command.Type == StoreTransactionFactType.Purchased
                && command.ExternalEventId.StartsWith("APPLE_SNAPSHOT:", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// 请求中的账户标识与权威账户标识不一致时拒绝认领交易。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Reject_Account_Mismatch_Without_Claiming_Transaction()
    {
        SetupAcquiredRequest();
        _appleStoreClient
            .Setup(x => x.VerifyTransactionAsync(
                It.IsAny<AppleStoreVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAppleTransaction(appAccountToken: Guid.NewGuid().ToString("D")));

        var result = await CreateHandler().Handle(
            CreateAppleCommand(appAccountToken: Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.DeterministicFailed, result.Outcome);
        Assert.Equal("PURCHASE_ACCOUNT_MISMATCH", result.FailureCode);
        _storeTransactionRepository.Verify(
            x => x.AddAsync(It.IsAny<StoreTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(x => x.Send(
            It.Is<CompleteIdempotentRequestCommand>(command =>
                command.ResultCode == "PURCHASE_ACCOUNT_MISMATCH"
                && command.StoreTransactionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 商店临时不可用时保留可重试失败语义并安排同键重试。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Mark_Retryable_When_Store_Is_Unavailable()
    {
        var leaseToken = SetupAcquiredRequest();
        _appleStoreClient
            .Setup(x => x.VerifyTransactionAsync(
                It.IsAny<AppleStoreVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "timeout"));

        var result = await CreateHandler().Handle(
            CreateAppleCommand(appAccountToken: Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.RetryableFailed, result.Outcome);
        Assert.Equal("STORE_SERVICE_UNAVAILABLE", result.FailureCode);
        Assert.True(result.RetryAfterSeconds > 0);
        _mediator.Verify(x => x.Send(
            It.Is<MarkIdempotentRequestRetryableFailedCommand>(command =>
                command.LeaseToken == leaseToken
                && command.ResultCode == "STORE_SERVICE_UNAVAILABLE"
                && command.NextRetryAt > Now),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(x => x.Send(
            It.IsAny<CompleteIdempotentRequestCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 相同幂等键对应不同请求摘要时稳定返回键复用错误。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Return_Idempotency_Key_Reused()
    {
        _mediator
            .Setup(x => x.Send(It.IsAny<AcquireIdempotentRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAcquireResult(
                result: IdempotentRequestAcquireResult.RequestHashMismatch,
                leaseToken: null));

        var result = await CreateHandler().Handle(
            CreateAppleCommand(appAccountToken: Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.DeterministicFailed, result.Outcome);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", result.FailureCode);
        _appleStoreClient.Verify(
            x => x.VerifyTransactionAsync(
                It.IsAny<AppleStoreVerificationRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// 相同幂等请求正在处理时返回处理中结果且不调用商店。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Return_Processing_When_Lease_Is_Active()
    {
        _mediator
            .Setup(x => x.Send(It.IsAny<AcquireIdempotentRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAcquireResult(
                result: IdempotentRequestAcquireResult.Processing,
                leaseToken: null));

        var result = await CreateHandler().Handle(
            CreateAppleCommand(appAccountToken: Guid.NewGuid().ToString("D")),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.Processing, result.Outcome);
        Assert.Equal("IDEMPOTENCY_REQUEST_PROCESSING", result.FailureCode);
        Assert.True(result.RetryAfterSeconds > 0);
        _appleStoreClient.Verify(
            x => x.VerifyTransactionAsync(
                It.IsAny<AppleStoreVerificationRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Google 消费成功后标记已验证并发放点数。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Grant_Points_After_Google_Consumption_Succeeds()
    {
        var accountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: accountToken);
        SetupProduct(points: 100);
        SetupPointAccount(availablePoints: 0);
        _googlePlayClient
            .Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(accountToken: accountToken));
        StoreTransaction? addedTransaction = null;
        _storeTransactionRepository
            .Setup(x => x.AddAsync(It.IsAny<StoreTransaction>(), It.IsAny<CancellationToken>()))
            .Callback((StoreTransaction transaction, CancellationToken _) => addedTransaction = transaction)
            .ReturnsAsync((StoreTransaction transaction, CancellationToken _) => transaction);

        var result = await CreateHandler().Handle(
            CreateGoogleCommand(accountToken: accountToken),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.Succeeded, result.Outcome);
        Assert.Equal(StoreTransactionStatus.Verified, result.Status);
        Assert.Equal(100, result.AvailablePoints);
        Assert.NotNull(addedTransaction);
        Assert.Equal("google-purchase-token", addedTransaction!.ExternalTransactionId);
        _googlePlayClient.Verify(
            x => x.ConsumeAsync(It.IsAny<GooglePlayConsumptionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Store == AppStore.GooglePlay
                && command.ExternalTransactionId == "google-purchase-token"
                && command.StoreOrderId == "GPA.3300-1234-5678-90123"
                && command.SourceType == StoreTransactionFactSourceType.ClientVerification
                && command.Type == StoreTransactionFactType.Purchased),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Google 客户端附带订单号时必须与平台权威订单号一致。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Reject_When_Google_Order_Id_Does_Not_Match()
    {
        var accountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: accountToken);
        _googlePlayClient
            .Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(accountToken: accountToken));

        var result = await CreateHandler().Handle(
            CreateGoogleCommand(accountToken) with
            {
                TransactionId = "GPA.client-order-id"
            },
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.DeterministicFailed, result.Outcome);
        Assert.Equal("GOOGLE_ORDER_ID_MISMATCH", result.FailureCode);
        _storeTransactionRepository.Verify(
            x => x.AddAsync(It.IsAny<StoreTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Google 消费临时失败时保持待消费且不发放点数。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Keep_Pending_Consumption_When_Consume_Fails()
    {
        var accountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: accountToken);
        SetupProduct(points: 100);
        SetupPointAccount(availablePoints: 0);
        _googlePlayClient
            .Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(accountToken: accountToken));
        _googlePlayClient
            .Setup(x => x.ConsumeAsync(
                It.IsAny<GooglePlayConsumptionRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreClientException(
                code: "PURCHASE_CONSUMPTION_PENDING",
                isRetryable: true,
                message: "unavailable"));

        var result = await CreateHandler().Handle(
            CreateGoogleCommand(accountToken: accountToken),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.Succeeded, result.Outcome);
        Assert.Equal(StoreTransactionStatus.PendingConsumption, result.Status);
        Assert.Equal(0, result.GrantedPoints);
        _mediator.Verify(x => x.Send(
            It.Is<CompleteIdempotentRequestCommand>(command =>
                command.ResultCode == "PURCHASE_CONSUMPTION_PENDING"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Google 延迟付款未完成时保持待验证。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Keep_Pending_Verification_For_Google_Delayed_Payment()
    {
        var accountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: accountToken);
        SetupProduct(points: 100);
        _googlePlayClient
            .Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(
                accountToken: accountToken,
                purchaseState: GooglePlayPurchaseState.Pending,
                purchasedAt: null));

        var result = await CreateHandler().Handle(
            CreateGoogleCommand(accountToken: accountToken),
            CancellationToken.None);

        Assert.Equal(StorePurchaseVerificationOutcome.Succeeded, result.Outcome);
        Assert.Equal(StoreTransactionStatus.PendingVerification, result.Status);
        Assert.Equal("PURCHASE_PENDING", result.FailureCode);
        _googlePlayClient.Verify(
            x => x.ConsumeAsync(It.IsAny<GooglePlayConsumptionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// 已入账 Apple 交易再次验到退款时完整冲正，余额不足允许返回负数。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Fully_Reverse_A_Refunded_Apple_Transaction()
    {
        var appAccountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: appAccountToken);
        SetupProduct(points: 100);
        SetupPointAccount(availablePoints: 15);
        var transaction = CreateVerifiedAppleStoreTransaction();
        _storeTransactionRepository
            .Setup(x => x.FindByExternalTransactionIdAsync(
                It.IsAny<AppStore>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _appleStoreClient
            .Setup(x => x.VerifyTransactionAsync(
                It.IsAny<AppleStoreVerificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAppleTransaction(
                appAccountToken: appAccountToken,
                isRevoked: true));

        var result = await CreateHandler().Handle(
            CreateAppleCommand(appAccountToken: appAccountToken),
            CancellationToken.None);

        Assert.Equal(StoreTransactionStatus.Refunded, result.Status);
        Assert.Equal(100, result.ReversedPoints);
        Assert.Equal(-85, result.AvailablePoints);
    }

    /// <summary>
    /// 已入账 Google 交易权威快照确认作废时完整冲正。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Fully_Reverse_A_Voided_Google_Transaction()
    {
        var accountToken = Guid.NewGuid().ToString("D");
        SetupAcquiredRequest();
        SetupPurchaseAccount(accountToken: accountToken);
        SetupProduct(points: 100);
        SetupPointAccount(availablePoints: 15);
        var transaction = CreateVerifiedGoogleStoreTransaction();
        _storeTransactionRepository
            .Setup(x => x.FindByExternalTransactionIdAsync(
                It.IsAny<AppStore>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _googlePlayClient
            .Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(
                accountToken: accountToken,
                refundableQuantity: 0));

        var result = await CreateHandler().Handle(
            CreateGoogleCommand(accountToken: accountToken),
            CancellationToken.None);

        Assert.Equal(StoreTransactionStatus.Refunded, result.Status);
        Assert.Equal(100, result.ReversedPoints);
        Assert.Equal(-85, result.AvailablePoints);
        _googlePlayClient.Verify(
            x => x.ConsumeAsync(It.IsAny<GooglePlayConsumptionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private VerifyStorePurchaseCommandHandler CreateHandler()
    {
        return new VerifyStorePurchaseCommandHandler(
            mediator: _mediator.Object,
            storeTransactionRepository: _storeTransactionRepository.Object,
            storeProductRepository: _storeProductRepository.Object,
            userAccountRepository: _userAccountRepository.Object,
            pointAccountRepository: _pointAccountRepository.Object,
            requestHasher: new StoreVerificationRequestHasher(),
            appleStoreClient: _appleStoreClient.Object,
            googlePlayClient: _googlePlayClient.Object,
            clock: _clock.Object);
    }

    private Guid SetupAcquiredRequest()
    {
        var leaseToken = Guid.NewGuid();
        _mediator
            .Setup(x => x.Send(It.IsAny<AcquireIdempotentRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAcquireResult(
                result: IdempotentRequestAcquireResult.Acquired,
                leaseToken: leaseToken));
        return leaseToken;
    }

    private static AcquireIdempotentRequestResult CreateAcquireResult(
        IdempotentRequestAcquireResult result,
        Guid? leaseToken)
    {
        return new AcquireIdempotentRequestResult(
            IdempotentRequestId: new IdempotentRequestId(Guid.NewGuid()),
            Result: result,
            LeaseToken: leaseToken,
            LeaseExpiresAt: Now.AddSeconds(30),
            NextRetryAt: null,
            StoreTransactionId: null,
            ResultCode: null);
    }

    private void SetupPurchaseAccount(string accountToken)
    {
        var userAccount = UserAccount.Register(emailAddress: "purchase-owner@example.com");
        SetUserAccountProperty(
            userAccount: userAccount,
            propertyName: nameof(UserAccount.Id),
            value: _userAccountId);
        SetUserAccountProperty(
            userAccount: userAccount,
            propertyName: nameof(UserAccount.PurchaseAccountToken),
            value: Guid.Parse(accountToken));
        _userAccountRepository
            .Setup(x => x.FindByPurchaseAccountTokenAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAccount);
    }

    private static void SetUserAccountProperty<T>(UserAccount userAccount, string propertyName, T value)
    {
        var property = typeof(UserAccount).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"未找到 UserAccount.{propertyName} 属性。");
        property.SetValue(userAccount, value);
    }

    private void SetupProduct(int points)
    {
        _storeProductRepository
            .Setup(x => x.FindByProductIdAsync(
                It.IsAny<AppStore>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePersistedStoreProduct(points: points));
    }

    private static StoreProduct CreatePersistedStoreProduct(int points)
    {
        var product = (StoreProduct)Activator.CreateInstance(
            type: typeof(StoreProduct),
            nonPublic: true)!;
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.Id), value: new StoreProductId(Guid.NewGuid()));
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.Store), value: AppStore.AppleAppStore);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.ProductId), value: ProductId);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.Amount), value: 4.99m);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.CurrencyCode), value: "USD");
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.Points), value: points);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.Enabled), value: true);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.SortOrder), value: 10);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.CreatedAt), value: Now);
        SetStoreProductProperty(product: product, propertyName: nameof(StoreProduct.UpdatedAt), value: Now);
        return product;
    }

    private static void SetStoreProductProperty<T>(StoreProduct product, string propertyName, T value)
    {
        var property = typeof(StoreProduct).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"未找到 StoreProduct.{propertyName} 属性。");
        property.SetValue(product, value);
    }

    private void SetupPointAccount(int availablePoints)
    {
        var account = PointAccount.Create(_userAccountId);
        if (availablePoints > 0)
        {
            account.GrantPurchasedPoints(
                points: availablePoints,
                businessReference: "REGISTRATION:GRANT",
                now: Now);
        }

        _pointAccountRepository
            .Setup(x => x.GetByUserAccountIdAsync(It.IsAny<UserAccountId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
    }

    private static AppleStoreTransaction CreateAppleTransaction(
        string appAccountToken,
        bool isRevoked = false)
    {
        return new AppleStoreTransaction(
            TransactionId: "2000000123456789",
            ProductId: ProductId,
            BundleId: "com.lumavill.photorescue.dev",
            Environment: "Sandbox",
            TransactionType: "Consumable",
            Quantity: 1,
            PurchasedAt: Now.AddMinutes(-1),
            SignedDate: Now,
            AppAccountToken: appAccountToken,
            Amount: 4.99m,
            CurrencyCode: "USD",
            IsRevoked: isRevoked,
            RevocationDate: isRevoked ? Now : null,
            PayloadHash: "payload-hash");
    }

    private static GooglePlayPurchase CreateGooglePurchase(
        string accountToken,
        GooglePlayPurchaseState purchaseState = GooglePlayPurchaseState.Purchased,
        DateTimeOffset? purchasedAt = null,
        int refundableQuantity = 1)
    {
        return new GooglePlayPurchase(
            ProductId: ProductId,
            PurchaseState: purchaseState,
            IsConsumed: false,
            Quantity: 1,
            RefundableQuantity: refundableQuantity,
            ObfuscatedExternalAccountId: accountToken,
            OrderId: "GPA.3300-1234-5678-90123",
            PurchaseCompletedAt: purchasedAt ?? Now.AddMinutes(-1),
            IsTestPurchase: false,
            IsAcknowledged: true,
            SnapshotHash: "snapshot-hash");
    }

    private StoreTransaction CreateVerifiedAppleStoreTransaction()
    {
        var transaction = StoreTransaction.CreateLinkedApple(
            externalTransactionId: "2000000123456789",
            userAccountId: _userAccountId,
            now: Now.AddMinutes(-2));
        transaction.CapturePurchase(
            storeProductId: new StoreProductId(Guid.NewGuid()),
            productId: ProductId,
            pointsSnapshot: 100,
            purchasedAt: Now.AddMinutes(-2),
            verificationPayloadHash: "old-payload-hash",
            amount: 4.99m,
            currencyCode: "USD",
            platformVersionAt: Now.AddMinutes(-1),
            now: Now.AddMinutes(-1));
        transaction.CompleteVerification(now: Now.AddMinutes(-1));
        return transaction;
    }

    private StoreTransaction CreateVerifiedGoogleStoreTransaction()
    {
        var transaction = StoreTransaction.CreateLinkedGoogle(
            externalTransactionId: "google-purchase-token",
            userAccountId: _userAccountId,
            isTestPurchase: false,
            now: Now.AddMinutes(-2));
        transaction.CapturePurchase(
            storeProductId: new StoreProductId(Guid.NewGuid()),
            productId: ProductId,
            pointsSnapshot: 100,
            purchasedAt: Now.AddMinutes(-2),
            verificationPayloadHash: "old-snapshot-hash",
            amount: null,
            currencyCode: null,
            platformVersionAt: null,
            now: Now.AddMinutes(-1));
        transaction.MarkPendingConsumption(now: Now.AddMinutes(-1));
        transaction.CompleteVerification(now: Now.AddMinutes(-1));
        return transaction;
    }

    private VerifyStorePurchaseCommand CreateAppleCommand(string appAccountToken)
    {
        return new VerifyStorePurchaseCommand(
            UserAccountId: _userAccountId,
            Store: AppStore.AppleAppStore,
            ProductId: ProductId,
            TransactionId: "2000000123456789",
            TransactionJws: "signed-transaction",
            PurchaseToken: null,
            ExternalAccountToken: appAccountToken,
            IdempotencyKey: "idempotency-key");
    }

    private VerifyStorePurchaseCommand CreateGoogleCommand(string accountToken)
    {
        return new VerifyStorePurchaseCommand(
            UserAccountId: _userAccountId,
            Store: AppStore.GooglePlay,
            ProductId: ProductId,
            TransactionId: null,
            TransactionJws: null,
            PurchaseToken: "google-purchase-token",
            ExternalAccountToken: accountToken,
            IdempotencyKey: "idempotency-key");
    }
}
