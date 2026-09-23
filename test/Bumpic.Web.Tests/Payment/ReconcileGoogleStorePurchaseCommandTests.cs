using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Moq;
using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransaction;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google Play 通知购买对账命令测试宿主。
/// </summary>
public class ReconcileGoogleStorePurchaseCommandTestFactory : MyWebApplicationFactory
{
    /// <summary>
    /// 隔离测试消息虚拟主机。
    /// </summary>
    protected override string TestVirtualHost { get; } = "google-reconcile-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Google Play 客户端测试替身。
    /// </summary>
    public Mock<IGooglePlayClient> GooglePlayClient { get; } = new();

    /// <inheritdoc />
    protected override void SetupMockService(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:Database", (100 + TestInstanceIndex).ToString());
        builder.UseSetting("RegisterJobs", "false");
        builder.UseSetting("Env:ServiceEnv", TestVirtualHost);
        builder.UseSetting("Database:InitializeMode", "EnsureCreated");
        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton(GooglePlayClient.Object)));
    }
}

/// <summary>
/// Google Play 通知购买对账命令集成测试。
/// </summary>
public class ReconcileGoogleStorePurchaseCommandTests(
    ReconcileGoogleStorePurchaseCommandTestFactory factory)
    : IClassFixture<ReconcileGoogleStorePurchaseCommandTestFactory>
{
    private static readonly DateTimeOffset PurchasedAt = new(2026, 9, 15, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// 权威账户可识别时认领、消费并发放商品点数。
    /// </summary>
    [Fact]
    public async Task Handle_AuthoritativeAccountFound_ConsumesAndGrantsPoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var purchaseToken = $"purchase-{Guid.NewGuid():N}";
        var productId = $"product-{Guid.NewGuid():N}";
        var userAccount = await SeedAsync(productId: productId, createUser: true);
        var purchase = CreatePurchase(
            productId: productId,
            accountToken: userAccount!.PurchaseAccountToken,
            isConsumed: false);
        factory.GooglePlayClient.Setup(x => x.ConsumeAsync(
                It.Is<GooglePlayConsumptionRequest>(request =>
                    request.ProductId == productId && request.PurchaseToken == purchaseToken),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var transactionId = await ImportAsync(purchaseToken: purchaseToken);
        await using var scope = factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new ReconcileGoogleStorePurchaseCommand(
            StoreTransactionId: transactionId,
            PurchaseToken: purchaseToken,
            Purchase: purchase), cancellationToken);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.Id == transactionId, cancellationToken);
        var pointAccount = await db.PointAccounts.SingleAsync(
            x => x.UserAccountId == userAccount.Id, cancellationToken);

        Assert.Equal(ReconcileGoogleStorePurchaseOutcome.Completed, result.Outcome);
        Assert.Equal(StoreTransactionStatus.Verified, transaction.Status);
        Assert.Equal(StoreTransactionOwnershipStatus.Linked, transaction.OwnershipStatus);
        Assert.Equal(userAccount.Id, transaction.UserAccountId);
        Assert.Equal(20, transaction.PointsSnapshot);
        Assert.Equal(20, transaction.GrantedPoints);
        Assert.Equal(20, pointAccount.AvailablePoints);
        factory.GooglePlayClient.Verify(x => x.ConsumeAsync(
            It.Is<GooglePlayConsumptionRequest>(request =>
                request.ProductId == productId && request.PurchaseToken == purchaseToken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 权威账户无法识别时保留未归属交易且不消费。
    /// </summary>
    [Fact]
    public async Task Handle_AuthoritativeAccountMissing_LeavesTransactionUnlinked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var purchaseToken = $"purchase-{Guid.NewGuid():N}";
        var productId = $"product-{Guid.NewGuid():N}";
        await SeedAsync(productId: productId, createUser: false);
        var transactionId = await ImportAsync(purchaseToken: purchaseToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new ReconcileGoogleStorePurchaseCommand(
            StoreTransactionId: transactionId,
            PurchaseToken: purchaseToken,
            Purchase: CreatePurchase(
                productId: productId,
                accountToken: Guid.NewGuid(),
                isConsumed: false)), cancellationToken);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.Id == transactionId, cancellationToken);
        Assert.Equal(ReconcileGoogleStorePurchaseOutcome.Unlinked, result.Outcome);
        Assert.Equal(StoreTransactionOwnershipStatus.Unlinked, transaction.OwnershipStatus);
        Assert.Null(transaction.UserAccountId);
        Assert.Equal(0, transaction.PointsSnapshot);
        factory.GooglePlayClient.Verify(x => x.ConsumeAsync(
            It.Is<GooglePlayConsumptionRequest>(request =>
                request.PurchaseToken == purchaseToken),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Google 临时消费失败时保存待消费状态并返回可重试结果。
    /// </summary>
    [Fact]
    public async Task Handle_ConsumeTemporarilyFails_PersistsPendingConsumption()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var purchaseToken = $"purchase-{Guid.NewGuid():N}";
        var productId = $"product-{Guid.NewGuid():N}";
        var userAccount = await SeedAsync(productId: productId, createUser: true);
        factory.GooglePlayClient.Setup(x => x.ConsumeAsync(
                It.Is<GooglePlayConsumptionRequest>(request => request.PurchaseToken == purchaseToken),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "temporary"));
        var transactionId = await ImportAsync(purchaseToken: purchaseToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new ReconcileGoogleStorePurchaseCommand(
            StoreTransactionId: transactionId,
            PurchaseToken: purchaseToken,
            Purchase: CreatePurchase(
                productId: productId,
                accountToken: userAccount!.PurchaseAccountToken,
                isConsumed: false)), cancellationToken);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.Id == transactionId, cancellationToken);
        Assert.Equal(ReconcileGoogleStorePurchaseOutcome.RetryableFailed, result.Outcome);
        Assert.Equal(StoreTransactionStatus.PendingConsumption, transaction.Status);
        Assert.Equal(20, transaction.PointsSnapshot);
        Assert.Equal(0, transaction.GrantedPoints);
        Assert.Equal(1, transaction.ConsumptionAttemptCount);
        Assert.NotNull(transaction.NextRetryAt);
    }

    private async Task<UserAccount?> SeedAsync(string productId, bool createUser)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = CreateProduct(productId: productId, points: 20);
        db.StoreProducts.Add(product);
        UserAccount? userAccount = null;
        if (createUser)
        {
            userAccount = UserAccount.Register($"{Guid.NewGuid():N}@example.test");
            db.UserAccounts.Add(userAccount);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return userAccount;
    }

    private async Task<Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransactionId> ImportAsync(
        string purchaseToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new ImportUnlinkedGoogleStoreTransactionCommand(
            ExternalTransactionId: purchaseToken,
            IsTestPurchase: true), TestContext.Current.CancellationToken);
    }

    private static GooglePlayPurchase CreatePurchase(
        string productId,
        Guid accountToken,
        bool isConsumed)
    {
        return new GooglePlayPurchase(
            ProductId: productId,
            PurchaseState: GooglePlayPurchaseState.Purchased,
            IsConsumed: isConsumed,
            Quantity: 1,
            RefundableQuantity: 1,
            ObfuscatedExternalAccountId: accountToken.ToString("D"),
            OrderId: "GPA.1234-5678-9012-34567",
            PurchaseCompletedAt: PurchasedAt,
            IsTestPurchase: true,
            IsAcknowledged: true,
            SnapshotHash: $"snapshot-{Guid.NewGuid():N}");
    }

    private static StoreProduct CreateProduct(string productId, int points)
    {
        var product = (StoreProduct)Activator.CreateInstance(typeof(StoreProduct), nonPublic: true)!;
        SetProperty(product: product, propertyName: nameof(StoreProduct.Id), value: new StoreProductId(Guid.NewGuid()));
        SetProperty(product: product, propertyName: nameof(StoreProduct.Store), value: AppStore.GooglePlay);
        SetProperty(product: product, propertyName: nameof(StoreProduct.ProductId), value: productId);
        SetProperty(product: product, propertyName: nameof(StoreProduct.Amount), value: 1.49m);
        SetProperty(product: product, propertyName: nameof(StoreProduct.CurrencyCode), value: "USD");
        SetProperty(product: product, propertyName: nameof(StoreProduct.Points), value: points);
        SetProperty(product: product, propertyName: nameof(StoreProduct.Enabled), value: true);
        SetProperty(product: product, propertyName: nameof(StoreProduct.SortOrder), value: 1);
        SetProperty(product: product, propertyName: nameof(StoreProduct.CreatedAt), value: PurchasedAt);
        SetProperty(product: product, propertyName: nameof(StoreProduct.UpdatedAt), value: PurchasedAt);
        return product;
    }

    private static void SetProperty<T>(StoreProduct product, string propertyName, T value)
    {
        var property = typeof(StoreProduct).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"未找到 StoreProduct.{propertyName} 属性。");
        property.SetValue(product, value);
    }
}
