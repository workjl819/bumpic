using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 商店交易领域测试。
/// </summary>
public class StoreTransactionTests
{
    /// <summary>
    /// Apple 任意退款类型在 P0 均完整冲正整笔已发放点数。
    /// </summary>
    [Fact]
    public void ApplyAppleRefund_Should_Reverse_All_Granted_Points()
    {
        var now = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var transaction = CreateVerifiedAppleTransaction(now: now);

        var firstDelta = transaction.ApplyAppleRefund(
            refundedAt: now.AddMinutes(1),
            platformVersionAt: now.AddMinutes(1),
            factKey: "refund-1",
            now: now.AddMinutes(1));
        var duplicateDelta = transaction.ApplyAppleRefund(
            refundedAt: now.AddMinutes(2),
            platformVersionAt: now.AddMinutes(2),
            factKey: "refund-2",
            now: now.AddMinutes(2));

        Assert.Equal(100, firstDelta);
        Assert.Equal(0, duplicateDelta);
        Assert.Equal(100, transaction.ReversedPoints);
        Assert.Equal(StoreTransactionStatus.Refunded, transaction.Status);
    }

    /// <summary>
    /// 退款撤回恢复整笔当前冲正点数，之后的新退款可以再次完整冲正。
    /// </summary>
    [Fact]
    public void ReinstateAppleRefund_Should_Restore_Full_Refund_And_Allow_Later_Refund()
    {
        var now = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var transaction = CreateVerifiedAppleTransaction(now: now);
        transaction.ApplyAppleRefund(
            refundedAt: now.AddMinutes(1),
            platformVersionAt: now.AddMinutes(1),
            factKey: "refund-1",
            now: now.AddMinutes(1));

        var reinstated = transaction.ReinstateAppleRefund(
            platformVersionAt: now.AddMinutes(2),
            factKey: "reversed-1",
            now: now.AddMinutes(2));
        var nextRefundDelta = transaction.ApplyAppleRefund(
            refundedAt: now.AddMinutes(3),
            platformVersionAt: now.AddMinutes(3),
            factKey: "refund-2",
            now: now.AddMinutes(3));

        Assert.Equal(100, reinstated);
        Assert.Equal(100, nextRefundDelta);
        Assert.Equal(100, transaction.GrantedPoints);
        Assert.Equal(100, transaction.ReversedPoints);
        Assert.Equal(StoreTransactionStatus.Refunded, transaction.Status);
    }

    /// <summary>
    /// 同一 Apple 签名时间的退款与退款撤回无论到达顺序都以退款撤回为准。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AppleFacts_With_Same_SignedDate_Should_Converge_To_RefundReversed(bool refundFirst)
    {
        var now = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var signedDate = now.AddMinutes(1);
        var transaction = CreateVerifiedAppleTransaction(now: now);

        if (refundFirst)
        {
            transaction.ApplyAppleRefund(
                refundedAt: signedDate,
                platformVersionAt: signedDate,
                factKey: "refund-same-time",
                now: signedDate);
            transaction.ReinstateAppleRefund(
                platformVersionAt: signedDate,
                factKey: "refund-reversed-same-time",
                now: signedDate);
        }
        else
        {
            transaction.ReinstateAppleRefund(
                platformVersionAt: signedDate,
                factKey: "refund-reversed-same-time",
                now: signedDate);
            transaction.ApplyAppleRefund(
                refundedAt: signedDate,
                platformVersionAt: signedDate,
                factKey: "refund-same-time",
                now: signedDate);
        }

        Assert.Equal(0, transaction.ReversedPoints);
        Assert.Equal(StoreTransactionStatus.Verified, transaction.Status);
        Assert.Equal(AppleTransactionFactPriority.RefundReversed, transaction.LastAppleFactPriority);
    }

    /// <summary>
    /// 未完成权威账户归属校验的交易不能保存购买快照。
    /// </summary>
    [Fact]
    public void CapturePurchase_Should_Reject_Unlinked_Transaction()
    {
        var now = DateTimeOffset.UtcNow;
        var transaction = StoreTransaction.CreateUnlinkedApple(
            externalTransactionId: "1000000000000001",
            now: now);

        Assert.Throws<InvalidOperationException>(() => transaction.CapturePurchase(
            storeProductId: new StoreProductId(Guid.NewGuid()),
            productId: "photorescue.points.small",
            pointsSnapshot: 100,
            purchasedAt: now,
            verificationPayloadHash: "payload-hash",
            amount: 1m,
            currencyCode: "USD",
            platformVersionAt: now,
            now: now));
    }

    /// <summary>
    /// Google 交易直接保存原始 purchaseToken 作为平台交易标识。
    /// </summary>
    [Fact]
    public void CreateLinkedGoogle_Should_Keep_PurchaseToken_As_ExternalTransactionId()
    {
        var now = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
        const string purchaseToken = "purchase-token.value_AO-J1-abc";

        var transaction = StoreTransaction.CreateLinkedGoogle(
            externalTransactionId: purchaseToken,
            userAccountId: new UserAccountId(Guid.NewGuid()),
            isTestPurchase: false,
            now: now);

        Assert.Equal(AppStore.GooglePlay, transaction.Store);
        Assert.Equal(purchaseToken, transaction.ExternalTransactionId);
        Assert.Equal(StoreTransactionOwnershipStatus.Linked, transaction.OwnershipStatus);
    }

    /// <summary>
    /// 可信 Google 通知可以先创建未归属交易，入账前全额退款将其安全作废。
    /// </summary>
    [Fact]
    public void ApplyGoogleFullRefund_Should_Void_Unlinked_NotGranted_Transaction()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var transaction = StoreTransaction.CreateUnlinkedGoogle(
            externalTransactionId: "google-purchase-token",
            isTestPurchase: true,
            now: now);

        var reversedPoints = transaction.ApplyGoogleFullRefund(
            refundedAt: now.AddMinutes(1),
            factKey: "google-refund-fact",
            now: now.AddMinutes(1));

        Assert.Equal(0, reversedPoints);
        Assert.Equal(StoreTransactionOwnershipStatus.Unlinked, transaction.OwnershipStatus);
        Assert.Equal(StoreTransactionStatus.Voided, transaction.Status);
        Assert.Equal(now.AddMinutes(1), transaction.RefundedAt);
    }

    /// <summary>
    /// 已合并的 Google 作废状态不能被迟到的取消快照降级覆盖。
    /// </summary>
    [Fact]
    public void ApplyGoogleCancellation_Should_Not_Overwrite_Voided_Status()
    {
        var now = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var transaction = StoreTransaction.CreateUnlinkedGoogle(
            externalTransactionId: "google-purchase-token",
            isTestPurchase: false,
            now: now);
        transaction.ApplyGoogleFullRefund(
            refundedAt: now.AddMinutes(1),
            factKey: "google-refund-fact",
            now: now.AddMinutes(1));

        transaction.ApplyGoogleCancellation(now: now.AddMinutes(2));

        Assert.Equal(StoreTransactionStatus.Voided, transaction.Status);
    }

    /// <summary>
    /// 平台交易标识只允许 ASCII 字符，保证唯一索引可建立。
    /// </summary>
    [Fact]
    public void CreateLinkedApple_Should_Reject_NonAscii_ExternalTransactionId()
    {
        var now = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => StoreTransaction.CreateLinkedApple(
            externalTransactionId: "交易-1000000000000001",
            userAccountId: new UserAccountId(Guid.NewGuid()),
            now: now));
    }

    /// <summary>
    /// 平台交易标识超过 1000 字符时拒绝保存。
    /// </summary>
    [Fact]
    public void CreateLinkedGoogle_Should_Reject_Overlong_ExternalTransactionId()
    {
        var now = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => StoreTransaction.CreateLinkedGoogle(
            externalTransactionId: new string('a', 1001),
            userAccountId: new UserAccountId(Guid.NewGuid()),
            isTestPurchase: false,
            now: now));
    }

    private static StoreTransaction CreateVerifiedAppleTransaction(DateTimeOffset now)
    {
        var transaction = StoreTransaction.CreateLinkedApple(
            externalTransactionId: "1000000000000001",
            userAccountId: new UserAccountId(Guid.NewGuid()),
            now: now);
        transaction.CapturePurchase(
            storeProductId: new StoreProductId(Guid.NewGuid()),
            productId: "photorescue.points.small",
            pointsSnapshot: 100,
            purchasedAt: now,
            verificationPayloadHash: "payload-hash",
            amount: 1m,
            currencyCode: "USD",
            platformVersionAt: now,
            now: now);
        transaction.CompleteVerification(now: now);
        return transaction;
    }
}
