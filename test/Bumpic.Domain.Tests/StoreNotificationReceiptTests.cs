using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 商店通知收件记录领域测试。
/// </summary>
public class StoreNotificationReceiptTests
{
    /// <summary>
    /// 收件记录应原样保留原始和规范化明文载荷。
    /// </summary>
    [Fact]
    public void Constructor_Should_Preserve_Plaintext_Payloads()
    {
        var now = DateTimeOffset.UtcNow;
        const string rawPayload = "  signed-payload  ";
        const string normalizedPayload = "{\"schemaVersion\":1}";

        var receipt = new StoreNotificationReceipt(
            store: AppStore.AppleAppStore,
            externalNotificationId: Guid.NewGuid().ToString("D"),
            notificationType: "REFUND",
            schemaVersion: 1,
            parserVersion: 1,
            rawPayload: rawPayload,
            normalizedPayload: normalizedPayload,
            payloadHash: new byte[32],
            sourceVerifiedAt: now,
            sourcePrincipal: "apple",
            sourceAudience: null,
            occurredAt: now,
            now: now);

        Assert.Equal(rawPayload, receipt.RawPayload);
        Assert.Equal(normalizedPayload, receipt.NormalizedPayload);
    }

    /// <summary>
    /// 处理中进程崩溃后，租约到期允许其他实例接管。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Allow_Takeover_After_Lease_Expires()
    {
        var now = DateTimeOffset.UtcNow;
        var receipt = CreateReceipt(now: now);

        var first = receipt.TryAcquire(
            leaseOwner: "worker-1",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now);
        var activeLease = receipt.TryAcquire(
            leaseOwner: "worker-2",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddSeconds(30));
        var takeover = receipt.TryAcquire(
            leaseOwner: "worker-2",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Equal(StoreNotificationReceiptAcquireResult.Acquired, first);
        Assert.Equal(StoreNotificationReceiptAcquireResult.Processing, activeLease);
        Assert.Equal(StoreNotificationReceiptAcquireResult.Acquired, takeover);
        Assert.Equal("worker-2", receipt.LeaseOwner);
        Assert.Equal(2, receipt.AttemptCount);
    }

    /// <summary>
    /// 可重试失败在退避到期前不能重新领取。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Respect_Retry_Schedule()
    {
        var now = DateTimeOffset.UtcNow;
        var receipt = CreateReceipt(now: now);
        receipt.TryAcquire(
            leaseOwner: "worker-1",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now);
        receipt.Fail(
            leaseOwner: "worker-1",
            failureCode: "STORE_SERVICE_UNAVAILABLE",
            nextRetryAt: now.AddMinutes(5),
            deadLetter: false,
            now: now.AddSeconds(10));

        var early = receipt.TryAcquire(
            leaseOwner: "worker-2",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(4));
        var due = receipt.TryAcquire(
            leaseOwner: "worker-2",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(5));

        Assert.Equal(StoreNotificationReceiptAcquireResult.RetryNotDue, early);
        Assert.Equal(StoreNotificationReceiptAcquireResult.Acquired, due);
    }

    /// <summary>
    /// 已完成通知不能被重复领取。
    /// </summary>
    [Fact]
    public void Complete_Should_Make_Receipt_Terminal()
    {
        var now = DateTimeOffset.UtcNow;
        var receipt = CreateReceipt(now: now);
        receipt.TryAcquire(
            leaseOwner: "worker-1",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now);
        receipt.Complete(leaseOwner: "worker-1", now: now.AddSeconds(1));

        var result = receipt.TryAcquire(
            leaseOwner: "worker-2",
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Equal(StoreNotificationReceiptAcquireResult.Processed, result);
        Assert.Equal(StoreNotificationReceiptStatus.Processed, receipt.Status);
        Assert.Null(receipt.LeaseOwner);
    }

    private static StoreNotificationReceipt CreateReceipt(DateTimeOffset now)
    {
        return new StoreNotificationReceipt(
            store: AppStore.AppleAppStore,
            externalNotificationId: Guid.NewGuid().ToString("D"),
            notificationType: "REFUND",
            schemaVersion: 1,
            parserVersion: 1,
            rawPayload: "signed-payload",
            normalizedPayload: "{}",
            payloadHash: new byte[32],
            sourceVerifiedAt: now,
            sourcePrincipal: "apple",
            sourceAudience: null,
            occurredAt: now,
            now: now);
    }
}
