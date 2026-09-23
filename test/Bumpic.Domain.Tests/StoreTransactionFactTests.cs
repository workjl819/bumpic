using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 商店交易事实领域测试。
/// </summary>
public class StoreTransactionFactTests
{
    /// <summary>
    /// 通知来源事实必须关联对应的收件记录。
    /// </summary>
    [Fact]
    public void Constructor_Should_Require_Receipt_For_Notification_Source()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => CreateFact(
            receiptId: null,
            sourceType: StoreTransactionFactSourceType.Notification,
            now: now));
    }

    /// <summary>
    /// 同一事实可以幂等标记到同一交易，但不能改绑其他交易。
    /// </summary>
    [Fact]
    public void MarkApplied_Should_Be_Idempotent_For_The_Same_Transaction()
    {
        var now = DateTimeOffset.UtcNow;
        var fact = CreateFact(
            receiptId: new StoreNotificationReceiptId(Guid.NewGuid()),
            sourceType: StoreTransactionFactSourceType.Notification,
            now: now);
        var transactionId = new StoreTransactionId(Guid.NewGuid());

        fact.MarkApplied(storeTransactionId: transactionId, now: now.AddSeconds(1));
        fact.MarkApplied(storeTransactionId: transactionId, now: now.AddSeconds(2));

        Assert.Equal(transactionId, fact.StoreTransactionId);
        Assert.Throws<InvalidOperationException>(() => fact.MarkApplied(
            storeTransactionId: new StoreTransactionId(Guid.NewGuid()),
            now: now.AddSeconds(3)));
    }

    /// <summary>
    /// Google 订单标识作为可选审计快照保存并规范化空白。
    /// </summary>
    [Fact]
    public void Constructor_Should_Preserve_Normalized_Store_Order_Id()
    {
        var fact = new StoreTransactionFact(
            storeNotificationReceiptId: null,
            sourceType: StoreTransactionFactSourceType.ClientVerification,
            store: AppStore.GooglePlay,
            externalTransactionId: "google-purchase-token",
            storeOrderId: " GPA.3300-1234-5678-90123 ",
            externalEventId: "google-snapshot-1",
            type: StoreTransactionFactType.Purchased,
            occurredAt: DateTimeOffset.UtcNow,
            platformVersionAt: null,
            observedAt: DateTimeOffset.UtcNow,
            factKey: Enumerable.Repeat((byte)1, 32).ToArray(),
            payloadHash: Enumerable.Repeat((byte)2, 32).ToArray(),
            now: DateTimeOffset.UtcNow);

        Assert.Equal("GPA.3300-1234-5678-90123", fact.StoreOrderId);
    }

    private static StoreTransactionFact CreateFact(
        StoreNotificationReceiptId? receiptId,
        StoreTransactionFactSourceType sourceType,
        DateTimeOffset now)
    {
        return new StoreTransactionFact(
            storeNotificationReceiptId: receiptId,
            sourceType: sourceType,
            store: AppStore.AppleAppStore,
            externalTransactionId: "1000000000000001",
            storeOrderId: null,
            externalEventId: "notification-1",
            type: StoreTransactionFactType.Refunded,
            occurredAt: now,
            platformVersionAt: now,
            observedAt: now,
            factKey: Enumerable.Repeat((byte)1, 32).ToArray(),
            payloadHash: Enumerable.Repeat((byte)2, 32).ToArray(),
            now: now);
    }
}
