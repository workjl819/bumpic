using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransaction;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Services.Store;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple 自动购买补偿测试宿主。
/// </summary>
public class AppleAutomaticPurchaseReconciliationTestFactory : MyWebApplicationFactory
{
    /// <summary>
    /// 隔离测试消息虚拟主机。
    /// </summary>
    protected override string TestVirtualHost { get; } = "apple-reconcile-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Apple 通知内层交易验签替身。
    /// </summary>
    public Mock<IAppleSignedPayloadVerifier> SignedPayloadVerifier { get; } = new();

    /// <summary>
    /// Apple 客户端验单替身。
    /// </summary>
    public Mock<IAppleStoreClient> AppleStoreClient { get; } = new();

    /// <inheritdoc />
    protected override void SetupMockService(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:Database", (200 + TestInstanceIndex).ToString());
        builder.UseSetting("RegisterJobs", "false");
        builder.UseSetting("Env:ServiceEnv", TestVirtualHost);
        builder.UseSetting("Database:InitializeMode", "EnsureCreated");
        builder.UseSetting("Payment:Apple:BundleId", AppleAutomaticPurchaseReconciliationTests.BundleId);
        builder.UseSetting("Payment:Apple:Environment", "Sandbox");
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(SignedPayloadVerifier.Object));
            services.Replace(ServiceDescriptor.Singleton(AppleStoreClient.Object));
        });
    }
}

/// <summary>
/// Apple ONE_TIME_CHARGE 自动认领和入账集成测试。
/// </summary>
public class AppleAutomaticPurchaseReconciliationTests(
    AppleAutomaticPurchaseReconciliationTestFactory factory)
    : IClassFixture<AppleAutomaticPurchaseReconciliationTestFactory>
{
    internal const string BundleId = "com.lumavill.photorescue.dev";
    private static readonly DateTimeOffset PurchasedAt = new(2026, 9, 16, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// ONE_TIME_CHARGE 的权威账户可识别时认领交易并只发放商品点数。
    /// </summary>
    [Fact]
    public async Task ProcessOneTimeCharge_AuthoritativeAccountFound_ClaimsAndGrantsPoints()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: true, createUser: true);
        var snapshot = CreateSnapshot(transactionId, productId, userAccount!.PurchaseAccountToken);
        var receiptId = await SeedNotificationAsync(snapshot);

        await ProcessNotificationAsync(receiptId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.ExternalTransactionId == transactionId,
            TestContext.Current.CancellationToken);
        var pointAccount = await db.PointAccounts.SingleAsync(
            x => x.UserAccountId == userAccount.Id,
            TestContext.Current.CancellationToken);
        var grants = await db.AccountPointRecords.CountAsync(
            x => x.UserAccountId == userAccount.Id && x.Type == AccountPointRecordType.PurchaseGranted,
            TestContext.Current.CancellationToken);

        Assert.Equal(StoreTransactionStatus.Verified, transaction.Status);
        Assert.Equal(StoreTransactionOwnershipStatus.Linked, transaction.OwnershipStatus);
        Assert.Equal(userAccount.Id, transaction.UserAccountId);
        Assert.Equal(20, transaction.GrantedPoints);
        Assert.Equal(20, pointAccount.AvailablePoints);
        Assert.Equal(1, grants);
    }

    /// <summary>
    /// appAccountToken 无法定位账户时交易保持未归属且不发放点数。
    /// </summary>
    [Fact]
    public async Task ProcessOneTimeCharge_AccountTokenUnknown_LeavesTransactionUnlinked()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        await SeedAsync(productId, createProduct: true, createUser: false);
        var receiptId = await SeedNotificationAsync(
            CreateSnapshot(transactionId, productId, Guid.NewGuid()));

        await ProcessNotificationAsync(receiptId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.ExternalTransactionId == transactionId,
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreTransactionStatus.PendingVerification, transaction.Status);
        Assert.Equal(StoreTransactionOwnershipStatus.Unlinked, transaction.OwnershipStatus);
        Assert.Null(transaction.UserAccountId);
        Assert.Equal(0, transaction.GrantedPoints);
        var businessReference = $"PURCHASE:{transaction.Id.Id:D}";
        Assert.False(await db.AccountPointRecords.AnyAsync(
            x => x.Type == AccountPointRecordType.PurchaseGranted
                 && x.BusinessReference == businessReference,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 多数量 Apple 购买不得通过自动补偿入账。
    /// </summary>
    [Fact]
    public async Task ProcessOneTimeCharge_QuantityNotOne_IsRejected()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: true, createUser: true);
        var receiptId = await SeedNotificationAsync(
            CreateSnapshot(transactionId, productId, userAccount!.PurchaseAccountToken, quantity: 2));

        var exception = await Assert.ThrowsAsync<StoreNotificationProcessingException>(() =>
            ProcessNotificationAsync(receiptId));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.False(exception.IsRetryable);
        await AssertPendingWithoutGrantAsync(transactionId, userAccount.Id);
    }

    /// <summary>
    /// 非消耗型商品不得通过自动补偿入账。
    /// </summary>
    [Fact]
    public async Task ProcessOneTimeCharge_NonConsumable_IsRejected()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: true, createUser: true);
        var receiptId = await SeedNotificationAsync(CreateSnapshot(
            transactionId,
            productId,
            userAccount!.PurchaseAccountToken,
            transactionType: "Non-Consumable"));

        var exception = await Assert.ThrowsAsync<StoreNotificationProcessingException>(() =>
            ProcessNotificationAsync(receiptId));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.False(exception.IsRetryable);
        await AssertPendingWithoutGrantAsync(transactionId, userAccount.Id);
    }

    /// <summary>
    /// 商品尚未初始化时保持 PendingVerification 并返回可重试失败。
    /// </summary>
    [Fact]
    public async Task ProcessOneTimeCharge_ProductMissing_RemainsPendingForRetry()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: false, createUser: true);
        var receiptId = await SeedNotificationAsync(
            CreateSnapshot(transactionId, productId, userAccount!.PurchaseAccountToken));

        var exception = await Assert.ThrowsAsync<StoreNotificationProcessingException>(() =>
            ProcessNotificationAsync(receiptId));

        Assert.Equal("STORE_PRODUCT_NOT_FOUND", exception.Code);
        Assert.True(exception.IsRetryable);
        await AssertPendingWithoutGrantAsync(transactionId, userAccount.Id);
    }

    /// <summary>
    /// 同一 Apple 交易的重复通知只能产生一次点数发放。
    /// </summary>
    [Fact]
    public async Task ProcessOneTimeCharge_DuplicateNotifications_GrantsOnlyOnce()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: true, createUser: true);
        var snapshot = CreateSnapshot(transactionId, productId, userAccount!.PurchaseAccountToken);
        var firstReceiptId = await SeedNotificationAsync(snapshot);
        var secondReceiptId = await SeedNotificationAsync(snapshot);

        await ProcessNotificationAsync(firstReceiptId);
        await ProcessNotificationAsync(secondReceiptId);

        await AssertSingleGrantAsync(transactionId, userAccount.Id);
    }

    /// <summary>
    /// 客户端验单与服务端通知并发时共享交易锁且只能完成一次入账。
    /// </summary>
    [Fact(Skip = "临时屏蔽")]
    public async Task ClientVerificationAndNotification_Concurrent_GrantOnlyOnce()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: true, createUser: true);
        var snapshot = CreateSnapshot(transactionId, productId, userAccount!.PurchaseAccountToken);
        var receiptId = await SeedNotificationAsync(snapshot);
        factory.AppleStoreClient.Setup(x => x.VerifyTransactionAsync(
                It.Is<AppleStoreVerificationRequest>(request => request.TransactionId == transactionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var notificationTask = ProcessNotificationAsync(receiptId);
        var clientTask = VerifyFromClientAsync(userAccount.Id, snapshot);
        await Task.WhenAll(notificationTask, clientTask);

        await AssertSingleGrantAsync(transactionId, userAccount.Id);
    }

    /// <summary>
    /// 已退款交易收到更旧的购买通知时不能恢复状态或再次发放点数。
    /// </summary>
    [Fact]
    public async Task ProcessOldOneTimeCharge_AfterRefund_DoesNotRestorePoints()
    {
        ResetMocks();
        var productId = NewProductId();
        var transactionId = NewTransactionId();
        var userAccount = await SeedAsync(productId, createProduct: true, createUser: true);
        var currentSnapshot = CreateSnapshot(
            transactionId,
            productId,
            userAccount!.PurchaseAccountToken,
            signedDate: PurchasedAt);
        var storeTransactionId = await ImportAsync(transactionId);

        await using (var commandScope = factory.Services.CreateAsyncScope())
        {
            var mediator = commandScope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new ReconcileAppleStorePurchaseCommand(
                storeTransactionId,
                currentSnapshot), TestContext.Current.CancellationToken);
            await mediator.Send(new ReverseStorePurchaseCommand(
                storeTransactionId,
                PurchasedAt.AddMinutes(2),
                PurchasedAt.AddMinutes(2),
                $"refund-{Guid.NewGuid():N}"), TestContext.Current.CancellationToken);
        }

        var oldSnapshot = currentSnapshot with
        {
            SignedDate = PurchasedAt.AddMinutes(-1),
            PayloadHash = $"old-{Guid.NewGuid():N}"
        };
        var receiptId = await SeedNotificationAsync(oldSnapshot);
        await ProcessNotificationAsync(receiptId);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.Id == storeTransactionId,
            TestContext.Current.CancellationToken);
        var pointAccount = await db.PointAccounts.SingleAsync(
            x => x.UserAccountId == userAccount.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreTransactionStatus.Refunded, transaction.Status);
        Assert.Equal(20, transaction.GrantedPoints);
        Assert.Equal(20, transaction.ReversedPoints);
        Assert.Equal(0, pointAccount.AvailablePoints);
        Assert.Equal(1, await db.AccountPointRecords.CountAsync(
            x => x.UserAccountId == userAccount.Id && x.Type == AccountPointRecordType.PurchaseGranted,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.AccountPointRecords.CountAsync(
            x => x.UserAccountId == userAccount.Id && x.Type == AccountPointRecordType.PurchaseReversed,
            TestContext.Current.CancellationToken));
    }

    private void ResetMocks()
    {
        factory.SignedPayloadVerifier.Reset();
        factory.AppleStoreClient.Reset();
    }

    private async Task<UserAccount?> SeedAsync(
        string productId,
        bool createProduct,
        bool createUser)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (createProduct)
        {
            db.StoreProducts.Add(CreateProduct(productId, 20));
        }

        UserAccount? userAccount = null;
        if (createUser)
        {
            userAccount = UserAccount.Register($"{Guid.NewGuid():N}@example.test");
            db.UserAccounts.Add(userAccount);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return userAccount;
    }

    private async Task<StoreNotificationReceiptId> SeedNotificationAsync(AppleStoreTransaction transaction)
    {
        var signedTransactionInfo = $"signed-{Guid.NewGuid():N}";
        var verifiedPayload = CreateSignedPayload(transaction);
        factory.SignedPayloadVerifier.Setup(x => x.VerifyTransactionPayloadAsync(
                signedTransactionInfo,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifiedPayload);
        var normalizedPayload = JsonSerializer.Serialize(new
        {
            payload = new
            {
                data = new
                {
                    signedTransactionInfo
                }
            }
        });
        var receipt = new StoreNotificationReceipt(
            store: AppStore.AppleAppStore,
            externalNotificationId: Guid.NewGuid().ToString("D"),
            notificationType: "ONE_TIME_CHARGE",
            schemaVersion: 1,
            parserVersion: 1,
            rawPayload: normalizedPayload,
            normalizedPayload: normalizedPayload,
            payloadHash: SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPayload)),
            sourceVerifiedAt: PurchasedAt,
            sourcePrincipal: "apple-test",
            sourceAudience: BundleId,
            occurredAt: transaction.SignedDate,
            now: PurchasedAt);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StoreNotificationReceipts.Add(receipt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return receipt.Id;
    }

    private async Task ProcessNotificationAsync(StoreNotificationReceiptId receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IStoreNotificationProcessor>();
        await processor.ProcessAsync(receiptId, TestContext.Current.CancellationToken);
    }

    private async Task VerifyFromClientAsync(UserAccountId userAccountId, AppleStoreTransaction transaction)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new VerifyStorePurchaseCommand(
            UserAccountId: userAccountId,
            Store: AppStore.AppleAppStore,
            ProductId: transaction.ProductId,
            TransactionId: transaction.TransactionId,
            TransactionJws: "client-transaction-jws",
            PurchaseToken: null,
            ExternalAccountToken: transaction.AppAccountToken,
            IdempotencyKey: Guid.NewGuid().ToString("N")), TestContext.Current.CancellationToken);
    }

    private async Task<Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransactionId> ImportAsync(
        string transactionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(
            new ImportUnlinkedAppleStoreTransactionCommand(transactionId),
            TestContext.Current.CancellationToken);
    }

    private async Task AssertPendingWithoutGrantAsync(string transactionId, UserAccountId userAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.ExternalTransactionId == transactionId,
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreTransactionStatus.PendingVerification, transaction.Status);
        Assert.Equal(0, transaction.GrantedPoints);
        Assert.False(await db.AccountPointRecords.AnyAsync(
            x => x.UserAccountId == userAccountId && x.Type == AccountPointRecordType.PurchaseGranted,
            TestContext.Current.CancellationToken));
    }

    private async Task AssertSingleGrantAsync(string transactionId, UserAccountId userAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transactions = await db.StoreTransactions
            .Where(x => x.ExternalTransactionId == transactionId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var pointAccount = await db.PointAccounts.SingleAsync(
            x => x.UserAccountId == userAccountId,
            TestContext.Current.CancellationToken);
        var grants = await db.AccountPointRecords.CountAsync(
            x => x.UserAccountId == userAccountId && x.Type == AccountPointRecordType.PurchaseGranted,
            TestContext.Current.CancellationToken);
        Assert.Single(transactions);
        Assert.Equal(StoreTransactionStatus.Verified, transactions[0].Status);
        Assert.Equal(20, pointAccount.AvailablePoints);
        Assert.Equal(1, grants);
    }

    private static AppleStoreTransaction CreateSnapshot(
        string transactionId,
        string productId,
        Guid appAccountToken,
        int quantity = 1,
        string transactionType = "Consumable",
        DateTimeOffset? signedDate = null)
    {
        return new AppleStoreTransaction(
            TransactionId: transactionId,
            ProductId: productId,
            BundleId: BundleId,
            Environment: "Sandbox",
            TransactionType: transactionType,
            Quantity: quantity,
            PurchasedAt: PurchasedAt.AddMinutes(-1),
            SignedDate: signedDate ?? PurchasedAt,
            AppAccountToken: appAccountToken.ToString("D"),
            Amount: 1.49m,
            CurrencyCode: "USD",
            IsRevoked: false,
            RevocationDate: null,
            PayloadHash: $"payload-{Guid.NewGuid():N}");
    }

    private static AppleSignedPayload CreateSignedPayload(AppleStoreTransaction transaction)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            transactionId = transaction.TransactionId,
            productId = transaction.ProductId,
            bundleId = transaction.BundleId,
            environment = transaction.Environment,
            type = transaction.TransactionType,
            quantity = transaction.Quantity,
            purchaseDate = transaction.PurchasedAt.ToUnixTimeMilliseconds(),
            signedDate = transaction.SignedDate.ToUnixTimeMilliseconds(),
            appAccountToken = transaction.AppAccountToken,
            price = 1490,
            currency = transaction.CurrencyCode
        });
        return new AppleSignedPayload(payload, transaction.PayloadHash, "apple-test");
    }

    private static StoreProduct CreateProduct(string productId, int points)
    {
        var product = (StoreProduct)Activator.CreateInstance(typeof(StoreProduct), nonPublic: true)!;
        SetProperty(product, nameof(StoreProduct.Id), new StoreProductId(Guid.NewGuid()));
        SetProperty(product, nameof(StoreProduct.Store), AppStore.AppleAppStore);
        SetProperty(product, nameof(StoreProduct.ProductId), productId);
        SetProperty(product, nameof(StoreProduct.Amount), 1.49m);
        SetProperty(product, nameof(StoreProduct.CurrencyCode), "USD");
        SetProperty(product, nameof(StoreProduct.Points), points);
        SetProperty(product, nameof(StoreProduct.Enabled), true);
        SetProperty(product, nameof(StoreProduct.SortOrder), 1);
        SetProperty(product, nameof(StoreProduct.CreatedAt), PurchasedAt);
        SetProperty(product, nameof(StoreProduct.UpdatedAt), PurchasedAt);
        return product;
    }

    private static void SetProperty<T>(StoreProduct product, string propertyName, T value)
    {
        var property = typeof(StoreProduct).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"未找到 StoreProduct.{propertyName} 属性。");
        property.SetValue(product, value);
    }

    private static string NewProductId() => $"com.lumavill.photorescue.dev.points-{Guid.NewGuid():N}";

    private static string NewTransactionId() => $"apple-{Guid.NewGuid():N}";
}
