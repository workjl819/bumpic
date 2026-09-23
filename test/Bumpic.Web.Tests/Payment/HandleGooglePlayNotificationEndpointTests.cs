using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Endpoints.StoreNotification;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google Play 商店通知 Endpoint 测试宿主。
/// </summary>
public class HandleGooglePlayNotificationEndpointTestFactory : MyWebApplicationFactory
{
    internal const string Audience = "https://photorescue.example.test/api/v1/store-notification/google/handle";
    internal const string ServiceAccountEmail = "pubsub-push@example.test";
    internal const string SigningKey = "photorescue-google-pubsub-test-signing-key-2026";

    /// <summary>
    /// 隔离测试消息虚拟主机。
    /// </summary>
    protected override string TestVirtualHost { get; } = "google-notification-endpoint-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Google 通知解析器替身。
    /// </summary>
    public Mock<IGooglePlayNotificationParser> Parser { get; } = new();

    /// <inheritdoc />
    protected override void SetupMockService(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:Database", (500 + TestInstanceIndex).ToString());
        builder.UseSetting("RegisterJobs", "false");
        builder.UseSetting("Env:ServiceEnv", TestVirtualHost);
        builder.UseSetting("Database:InitializeMode", "EnsureCreated");
        builder.UseSetting("Payment:Google:PackageName", "com.lumavill.photorescue.dev");
        builder.UseSetting("Payment:Google:PushAudience", Audience);
        builder.UseSetting("Payment:Google:PushServiceAccountEmail", ServiceAccountEmail);
        builder.UseSetting("Payment:Google:PushOidcAuthority", string.Empty);
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(Parser.Object));
            services.PostConfigure<JwtBearerOptions>(
                HandleGooglePlayNotificationEndpoint.AuthenticationScheme,
                options =>
                {
                    options.Authority = null;
                    options.MetadataAddress = string.Empty;
                    options.TokenValidationParameters.ValidIssuer = "accounts.google.com";
                    options.TokenValidationParameters.ValidIssuers = ["accounts.google.com"];
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(SigningKey));
                    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                });
        });
    }

    /// <summary>
    /// 生成测试专用的 Google Pub/Sub OIDC Bearer Token。
    /// </summary>
    public string CreatePushToken(string email = ServiceAccountEmail)
    {
        var credentials = new SigningCredentials(
            key: new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            algorithm: SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "accounts.google.com",
            audience: Audience,
            claims:
            [
                new Claim("email", email),
                new Claim("email_verified", "true")
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Google Play 商店通知 Endpoint 集成测试。
/// </summary>
public class HandleGooglePlayNotificationEndpointTests(HandleGooglePlayNotificationEndpointTestFactory factory)
    : IClassFixture<HandleGooglePlayNotificationEndpointTestFactory>
{
    /// <summary>
    /// 缺少 Google Pub/Sub OIDC 身份时返回 401。
    /// </summary>
    [Fact]
    public async Task Handle_WithoutOidcToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await PostAsync(client: client, messageId: Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// OIDC 身份和通知均有效时写入 durable inbox 并返回 204。
    /// </summary>
    [Fact]
    public async Task Handle_VerifiedPush_PersistsReceiptAndReturnsNoContent()
    {
        factory.Parser.Reset();
        var messageId = Guid.NewGuid().ToString("N");
        factory.Parser.Setup(x => x.Parse(
                messageId,
                "encoded-data",
                It.IsAny<DateTimeOffset?>()))
            .Returns(new GooglePlayNotificationEnvelope(
                ExternalNotificationId: messageId,
                NotificationType: "ONE_TIME_PRODUCT_PURCHASED",
                SchemaVersion: 1,
                ParserVersion: 1,
                NormalizedPayload: "{\"schemaVersion\":1}",
                OccurredAt: DateTimeOffset.UtcNow,
                BelongsToCurrentDeployment: true));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            scheme: "Bearer",
            parameter: factory.CreatePushToken());

        var response = await PostAsync(client: client, messageId: messageId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var receipt = await db.StoreNotificationReceipts.SingleAsync(
            x => x.Store == AppStore.GooglePlay && x.ExternalNotificationId == messageId,
            TestContext.Current.CancellationToken);
        Assert.Equal(StoreNotificationReceiptStatus.Received, receipt.Status);
        Assert.Contains("encoded-data", receipt.RawPayload, StringComparison.Ordinal);
    }

    /// <summary>
    /// OIDC Token 中的服务账号邮箱不在允许列表时返回 403 且不写入 inbox。
    /// </summary>
    [Fact]
    public async Task Handle_UnapprovedServiceAccount_ReturnsForbiddenWithoutReceipt()
    {
        var messageId = Guid.NewGuid().ToString("N");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            scheme: "Bearer",
            parameter: factory.CreatePushToken(email: "other@example.test"));

        var response = await PostAsync(client: client, messageId: messageId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.StoreNotificationReceipts.AnyAsync(
            x => x.ExternalNotificationId == messageId,
            TestContext.Current.CancellationToken));
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string messageId)
    {
        return client.PostAsJsonAsync(
            "/api/v1/store-notification/google/handle",
            new HandleGooglePlayNotificationRequest(
                Message: new GooglePubSubMessage(
                    MessageId: messageId,
                    Data: "encoded-data",
                    PublishTime: DateTimeOffset.UtcNow),
                Subscription: "projects/test/subscriptions/payment"),
            TestContext.Current.CancellationToken);
    }
}
