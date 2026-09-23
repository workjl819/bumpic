using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Endpoints.StoreNotification;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple 商店通知 Endpoint 测试宿主。
/// </summary>
public class HandleAppleStoreNotificationEndpointTestFactory : MyWebApplicationFactory
{
    /// <summary>
    /// 隔离测试消息虚拟主机。
    /// </summary>
    protected override string TestVirtualHost { get; } = "apple-notification-endpoint-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Apple 通知解析器替身。
    /// </summary>
    public Mock<IAppleStoreNotificationParser> Parser { get; } = new();

    /// <inheritdoc />
    protected override void SetupMockService(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:Database", (400 + TestInstanceIndex).ToString());
        builder.UseSetting("RegisterJobs", "false");
        builder.UseSetting("Env:ServiceEnv", TestVirtualHost);
        builder.UseSetting("Database:InitializeMode", "EnsureCreated");
        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton(Parser.Object)));
    }
}

/// <summary>
/// Apple 商店通知 Endpoint 集成测试。
/// </summary>
public class HandleAppleStoreNotificationEndpointTests(HandleAppleStoreNotificationEndpointTestFactory factory)
    : IClassFixture<HandleAppleStoreNotificationEndpointTestFactory>
{
    /// <summary>
    /// 验签通过并成功写入 durable inbox 后返回 204。
    /// </summary>
    [Fact]
    public async Task Handle_VerifiedNotification_PersistsReceiptAndReturnsNoContent()
    {
        factory.Parser.Reset();
        var notificationId = Guid.NewGuid().ToString("D");
        factory.Parser.Setup(x => x.ParseAsync("signed-payload", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleStoreNotificationEnvelope(
                ExternalNotificationId: notificationId,
                NotificationType: "ONE_TIME_CHARGE",
                SchemaVersion: 2,
                ParserVersion: 1,
                NormalizedPayload: "{\"schemaVersion\":2}",
                OccurredAt: DateTimeOffset.UtcNow,
                SourcePrincipal: "apple",
                BelongsToCurrentDeployment: true));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/store-notification/apple/handle",
            new HandleAppleStoreNotificationRequest(SignedPayload: "signed-payload"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var receipt = await db.StoreNotificationReceipts.SingleAsync(
            x => x.Store == AppStore.AppleAppStore && x.ExternalNotificationId == notificationId,
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreNotificationReceiptStatus.Received, receipt.Status);
        Assert.Contains("signed-payload", receipt.RawPayload, StringComparison.Ordinal);
    }

    /// <summary>
    /// 已验签但不属于当前部署的通知返回 204 且不写入支付 inbox。
    /// </summary>
    [Fact]
    public async Task Handle_DeploymentMismatch_ReturnsNoContentWithoutReceipt()
    {
        factory.Parser.Reset();
        var notificationId = Guid.NewGuid().ToString("D");
        factory.Parser.Setup(x => x.ParseAsync("foreign-payload", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleStoreNotificationEnvelope(
                ExternalNotificationId: notificationId,
                NotificationType: "REFUND",
                SchemaVersion: 2,
                ParserVersion: 1,
                NormalizedPayload: string.Empty,
                OccurredAt: DateTimeOffset.UtcNow,
                SourcePrincipal: "apple",
                BelongsToCurrentDeployment: false));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/store-notification/apple/handle",
            new HandleAppleStoreNotificationRequest(SignedPayload: "foreign-payload"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.StoreNotificationReceipts.AnyAsync(
            x => x.ExternalNotificationId == notificationId,
            TestContext.Current.CancellationToken));
    }
}
