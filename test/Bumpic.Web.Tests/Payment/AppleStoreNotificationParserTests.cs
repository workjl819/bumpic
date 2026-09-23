using System.Text.Json;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple 服务端通知解析器测试。
/// </summary>
public class AppleStoreNotificationParserTests
{
    private const string BundleId = "com.lumavill.photorescue.dev";
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 11, 3, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 合法通知返回规范化的驼峰字段载荷，供后台处理器读取签名交易。
    /// </summary>
    [Fact]
    public async Task ParseAsync_Should_Normalize_Verified_Notification()
    {
        var notificationUuid = Guid.NewGuid();
        var signed = TestAppleNotificationSigner.Sign(
            bundleId: BundleId,
            environment: "Sandbox",
            notificationType: "REFUND",
            notificationUuid: notificationUuid,
            signedTransactionInfo: "nested-transaction-jws",
            signedDate: OccurredAt.ToUnixTimeMilliseconds());
        var parser = CreateParser(options: signed.Options);

        var envelope = await parser.ParseAsync(
            signedPayload: signed.Jws,
            cancellationToken: CancellationToken.None);

        Assert.Equal("REFUND", envelope.NotificationType);
        Assert.Equal(notificationUuid.ToString("D"), envelope.ExternalNotificationId);
        Assert.True(envelope.BelongsToCurrentDeployment);
        Assert.Equal(OccurredAt.ToUnixTimeMilliseconds(), envelope.OccurredAt.ToUnixTimeMilliseconds());
        Assert.False(string.IsNullOrWhiteSpace(envelope.SourcePrincipal));

        using var normalized = JsonDocument.Parse(envelope.NormalizedPayload);
        var payload = normalized.RootElement.GetProperty("payload");
        Assert.Equal("REFUND", payload.GetProperty("notificationType").GetString());
        Assert.Equal(
            notificationUuid.ToString("D"),
            payload.GetProperty("notificationUUID").GetString());
        Assert.Equal(
            "nested-transaction-jws",
            payload.GetProperty("data").GetProperty("signedTransactionInfo").GetString());
        Assert.Equal(
            BundleId,
            payload.GetProperty("data").GetProperty("bundleId").GetString());
    }

    /// <summary>
    /// 通知环境不属于当前部署时审计并返回 2xx，不进入 inbox。
    /// </summary>
    [Fact]
    public async Task ParseAsync_Should_Mark_Notification_From_Other_Environment_As_Foreign()
    {
        var signed = TestAppleNotificationSigner.Sign(
            bundleId: BundleId,
            environment: "Production",
            notificationType: "REFUND",
            notificationUuid: Guid.NewGuid(),
            signedTransactionInfo: "nested-transaction-jws",
            signedDate: OccurredAt.ToUnixTimeMilliseconds(),
            deploymentEnvironment: "Sandbox");
        var parser = CreateParser(options: signed.Options);

        var envelope = await parser.ParseAsync(
            signedPayload: signed.Jws,
            cancellationToken: CancellationToken.None);

        Assert.False(envelope.BelongsToCurrentDeployment);
        Assert.Equal("REFUND", envelope.NotificationType);
        Assert.Empty(envelope.NormalizedPayload);
    }

    /// <summary>
    /// 通知应用不属于当前部署时审计并返回 2xx，不进入 inbox。
    /// </summary>
    [Fact]
    public async Task ParseAsync_Should_Mark_Notification_From_Other_Bundle_As_Foreign()
    {
        var signed = TestAppleNotificationSigner.Sign(
            bundleId: "com.other.application",
            environment: "Sandbox",
            notificationType: "REFUND",
            notificationUuid: Guid.NewGuid(),
            signedTransactionInfo: "nested-transaction-jws",
            signedDate: OccurredAt.ToUnixTimeMilliseconds());
        var parser = CreateParser(options: signed.Options);

        var envelope = await parser.ParseAsync(
            signedPayload: signed.Jws,
            cancellationToken: CancellationToken.None);

        Assert.False(envelope.BelongsToCurrentDeployment);
    }

    /// <summary>
    /// 伪造为其他部署的通知仍必须先通过 Apple SDK 验签。
    /// </summary>
    [Fact]
    public async Task ParseAsync_Should_Reject_Tampered_Notification_From_Other_Deployment()
    {
        var signed = TestAppleNotificationSigner.Sign(
            bundleId: "com.other.application",
            environment: "Production",
            notificationType: "REFUND",
            notificationUuid: Guid.NewGuid(),
            signedTransactionInfo: "nested-transaction-jws",
            signedDate: OccurredAt.ToUnixTimeMilliseconds());
        var segments = signed.Jws.Split('.');
        var tampered = $"{segments[0]}.{segments[1][..^2]}AA.{segments[2]}";
        var parser = CreateParser(options: signed.Options);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            parser.ParseAsync(
                signedPayload: tampered,
                cancellationToken: CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
    }

    /// <summary>
    /// 签名被篡改时拒绝解析通知。
    /// </summary>
    [Fact]
    public async Task ParseAsync_Should_Reject_Tampered_Notification()
    {
        var signed = TestAppleNotificationSigner.Sign(
            bundleId: BundleId,
            environment: "Sandbox",
            notificationType: "REFUND",
            notificationUuid: Guid.NewGuid(),
            signedTransactionInfo: "nested-transaction-jws",
            signedDate: OccurredAt.ToUnixTimeMilliseconds());
        var segments = signed.Jws.Split('.');
        var tampered = $"{segments[0]}.{segments[1][..^2]}AA.{segments[2]}";
        var parser = CreateParser(options: signed.Options);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            parser.ParseAsync(
                signedPayload: tampered,
                cancellationToken: CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
    }

    private static AppleStoreNotificationParser CreateParser(AppleStoreOptions options)
    {
        var wrappedOptions = Microsoft.Extensions.Options.Options.Create(options);
        return new AppleStoreNotificationParser(
            signedPayloadVerifier: new MimoAppleStoreClient(
                payloadVerifier: new AppleSignedDataPayloadVerifier(wrappedOptions),
                appStoreServerApiClient: new StubAppleAppStoreServerApiClient(options)),
            options: wrappedOptions);
    }
}
