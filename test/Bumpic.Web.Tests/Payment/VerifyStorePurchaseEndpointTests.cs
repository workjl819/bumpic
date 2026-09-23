using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Endpoints.StorePurchase;
using Bumpic.Web.Tests.Extensions;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 商店验单 Endpoint 测试宿主。
/// </summary>
public class VerifyStorePurchaseEndpointTestFactory : MyWebApplicationFactory
{
    /// <summary>
    /// 隔离测试消息虚拟主机。
    /// </summary>
    protected override string TestVirtualHost { get; } = "verify-store-endpoint-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Apple 权威验单客户端替身。
    /// </summary>
    public Mock<IAppleStoreClient> AppleStoreClient { get; } = new();

    /// <inheritdoc />
    protected override void SetupMockService(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:Database", (300 + TestInstanceIndex).ToString());
        builder.UseSetting("RegisterJobs", "false");
        builder.UseSetting("Env:ServiceEnv", TestVirtualHost);
        builder.UseSetting("Database:InitializeMode", "EnsureCreated");
        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton(AppleStoreClient.Object)));
    }
}

/// <summary>
/// 商店验单 Endpoint 集成测试。
/// </summary>
public class VerifyStorePurchaseEndpointTests(VerifyStorePurchaseEndpointTestFactory factory)
    : IClassFixture<VerifyStorePurchaseEndpointTestFactory>
{
    private static readonly DateTimeOffset PurchasedAt = new(2026, 9, 16, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// 未登录用户不能调用验单接口。
    /// </summary>
    [Fact]
    public async Task Handle_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/store-transaction/verify",
            CreateRequest(productId: "product", transactionId: "transaction"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Apple 权威验单成功时返回已验证状态并完成点数入账。
    /// </summary>
    [Fact]
    public async Task Handle_ValidApplePurchase_ReturnsVerifiedAndGrantsPoints()
    {
        factory.AppleStoreClient.Reset();
        var productId = $"com.lumavill.photorescue.dev.points-{Guid.NewGuid():N}";
        var transactionId = $"apple-{Guid.NewGuid():N}";
        var user = await SeedAsync(productId: productId);
        factory.AppleStoreClient.Setup(x => x.VerifyTransactionAsync(
                It.Is<AppleStoreVerificationRequest>(request => request.TransactionId == transactionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleStoreTransaction(
                TransactionId: transactionId,
                ProductId: productId,
                BundleId: "com.lumavill.photorescue.dev",
                Environment: "Sandbox",
                TransactionType: "Consumable",
                Quantity: 1,
                PurchasedAt: PurchasedAt,
                SignedDate: PurchasedAt.AddMinutes(1),
                AppAccountToken: user.PurchaseAccountToken.ToString("D"),
                Amount: 1.49m,
                CurrencyCode: "USD",
                IsRevoked: false,
                RevocationDate: null,
                PayloadHash: $"payload-{Guid.NewGuid():N}"));
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, user);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await client.PostAsJsonAsync(
            "/api/v1/store-transaction/verify",
            CreateRequest(
                productId: productId,
                transactionId: transactionId,
                purchaseAccountToken: user.PurchaseAccountToken),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Verified", body, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = await db.StoreTransactions.SingleAsync(
            x => x.ExternalTransactionId == transactionId,
            TestContext.Current.CancellationToken);
        var account = await db.PointAccounts.SingleAsync(
            x => x.UserAccountId == user.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreTransactionStatus.Verified, transaction.Status);
        Assert.Equal(20, account.AvailablePoints);
    }

    /// <summary>
    /// 为测试客户端签发用户访问令牌。
    /// </summary>
    private async Task AuthenticateAsync(HttpClient client, UserAccount user)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var token = await scope.ServiceProvider.GetRequiredService<JwtGenerator>().Generate(new UserData
        {
            Id = user.Id.Id,
            Phone = string.Empty,
            PhoneRegion = string.Empty,
            Email = user.EmailAddress
        });
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token.AccessToken);
    }

    private async Task<UserAccount> SeedAsync(string productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = UserAccount.Register(emailAddress: $"{Guid.NewGuid():N}@example.test");
        db.UserAccounts.Add(user);
        db.StoreProducts.Add(CreateProduct(productId: productId));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private static VerifyStorePurchaseRequest CreateRequest(
        string productId,
        string transactionId,
        Guid? purchaseAccountToken = null)
    {
        return new VerifyStorePurchaseRequest(
            Store: AppStore.AppleAppStore,
            ProductId: productId,
            TransactionId: transactionId,
            TransactionJws: "signed-transaction",
            PurchaseToken: null,
            AppAccountToken: purchaseAccountToken?.ToString("D"),
            ObfuscatedAccountId: null);
    }

    private static StoreProduct CreateProduct(string productId)
    {
        var product = (StoreProduct)Activator.CreateInstance(typeof(StoreProduct), nonPublic: true)!;
        SetProperty(product: product, propertyName: nameof(StoreProduct.Store), value: AppStore.AppleAppStore);
        SetProperty(product: product, propertyName: nameof(StoreProduct.ProductId), value: productId);
        SetProperty(product: product, propertyName: nameof(StoreProduct.Amount), value: 1.49m);
        SetProperty(product: product, propertyName: nameof(StoreProduct.CurrencyCode), value: "USD");
        SetProperty(product: product, propertyName: nameof(StoreProduct.Points), value: 20);
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
