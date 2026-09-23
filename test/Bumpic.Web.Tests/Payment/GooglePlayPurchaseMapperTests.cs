using Google.Apis.AndroidPublisher.v3.Data;
using Bumpic.Web.Clients.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// ProductPurchaseV2 权威快照映射测试。
/// </summary>
public class GooglePlayPurchaseMapperTests
{
    private const string ProductId = "photorescue.points.small";
    private static readonly DateTimeOffset PurchaseCompletedAt =
        new(2026, 9, 11, 5, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 已支付未消费的购买快照映射为等待消费状态。
    /// </summary>
    [Fact]
    public void Map_Should_Translate_Purchased_Purchase()
    {
        var purchase = CreatePurchase(
            purchaseState: "PURCHASED",
            consumptionState: "CONSUMPTION_STATE_YET_TO_BE_CONSUMED");

        var mapped = GooglePlayPurchaseMapper.Map(purchase: purchase);

        Assert.Equal(ProductId, mapped.ProductId);
        Assert.Equal(GooglePlayPurchaseState.Purchased, mapped.PurchaseState);
        Assert.False(mapped.IsConsumed);
        Assert.Equal(1, mapped.Quantity);
        Assert.Equal(1, mapped.RefundableQuantity);
        Assert.Equal("account-hash", mapped.ObfuscatedExternalAccountId);
        Assert.Equal("GPA.3300-1234-5678-90123", mapped.OrderId);
        Assert.Equal(PurchaseCompletedAt, mapped.PurchaseCompletedAt);
        Assert.False(mapped.IsTestPurchase);
        Assert.True(mapped.IsAcknowledged);
        Assert.False(string.IsNullOrWhiteSpace(mapped.SnapshotHash));
    }

    /// <summary>
    /// 相同权威状态生成相同快照摘要，保证事实去重稳定。
    /// </summary>
    [Fact]
    public void Map_Should_Produce_Stable_Snapshot_Hash()
    {
        var first = GooglePlayPurchaseMapper.Map(CreatePurchase(purchaseState: "PURCHASED"));
        var second = GooglePlayPurchaseMapper.Map(CreatePurchase(purchaseState: "PURCHASED"));

        Assert.Equal(first.SnapshotHash, second.SnapshotHash);
    }

    /// <summary>
    /// 延迟付款与取消状态分别映射。
    /// </summary>
    [Theory]
    [InlineData("PENDING", GooglePlayPurchaseState.Pending)]
    [InlineData("CANCELLED", GooglePlayPurchaseState.Canceled)]
    public void Map_Should_Translate_Pending_And_Canceled(
        string purchaseState,
        GooglePlayPurchaseState expected)
    {
        var mapped = GooglePlayPurchaseMapper.Map(
            CreatePurchase(purchaseState: purchaseState));

        Assert.Equal(expected, mapped.PurchaseState);
    }

    /// <summary>
    /// 已消费的购买快照标记为已消费。
    /// </summary>
    [Fact]
    public void Map_Should_Mark_Consumed_Purchase()
    {
        var mapped = GooglePlayPurchaseMapper.Map(
            CreatePurchase(
                purchaseState: "PURCHASED",
                consumptionState: "CONSUMPTION_STATE_CONSUMED"));

        Assert.True(mapped.IsConsumed);
    }

    /// <summary>
    /// 存在测试购买上下文的购买标记为测试购买。
    /// </summary>
    [Fact]
    public void Map_Should_Mark_Test_Purchase()
    {
        var purchase = CreatePurchase(purchaseState: "PURCHASED");
        purchase.TestPurchaseContext = new TestPurchaseContext();

        var mapped = GooglePlayPurchaseMapper.Map(purchase: purchase);

        Assert.True(mapped.IsTestPurchase);
    }

    /// <summary>
    /// 缺少商品行的权威结果视为无效购买。
    /// </summary>
    [Fact]
    public void Map_Should_Reject_Missing_Line_Item()
    {
        var purchase = CreatePurchase(purchaseState: "PURCHASED");
        purchase.ProductLineItem = [];

        var exception = Assert.Throws<StoreClientException>(() =>
            GooglePlayPurchaseMapper.Map(purchase: purchase));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.False(exception.IsRetryable);
    }

    /// <summary>
    /// P0 不支持一笔购买包含多个商品行，避免只读取第一行错误入账。
    /// </summary>
    [Fact]
    public void Map_Should_Reject_Multiple_Line_Items()
    {
        var purchase = CreatePurchase(purchaseState: "PURCHASED");
        purchase.ProductLineItem!.Add(purchase.ProductLineItem[0]);

        var exception = Assert.Throws<StoreClientException>(() =>
            GooglePlayPurchaseMapper.Map(purchase: purchase));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
    }

    /// <summary>
    /// 未知消费状态不得被默认为未消费状态。
    /// </summary>
    [Fact]
    public void Map_Should_Reject_Unknown_Consumption_State()
    {
        var exception = Assert.Throws<StoreClientException>(() =>
            GooglePlayPurchaseMapper.Map(CreatePurchase(
                purchaseState: "PURCHASED",
                consumptionState: "CONSUMPTION_STATE_UNSPECIFIED")));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
    }

    /// <summary>
    /// 未知购买状态视为无效购买，不猜测业务状态。
    /// </summary>
    [Fact]
    public void Map_Should_Reject_Unknown_Purchase_State()
    {
        var exception = Assert.Throws<StoreClientException>(() =>
            GooglePlayPurchaseMapper.Map(CreatePurchase(purchaseState: "UNKNOWN")));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
    }

    private static ProductPurchaseV2 CreatePurchase(
        string purchaseState,
        string consumptionState = "CONSUMPTION_STATE_YET_TO_BE_CONSUMED")
    {
        return new ProductPurchaseV2
        {
            PurchaseStateContext = new PurchaseStateContext { PurchaseState = purchaseState },
            ProductLineItem =
            [
                new ProductLineItem
                {
                    ProductId = ProductId,
                    ProductOfferDetails = new ProductOfferDetails
                    {
                        ConsumptionState = consumptionState,
                        Quantity = 1,
                        RefundableQuantity = 1
                    }
                }
            ],
            ObfuscatedExternalAccountId = "account-hash",
            OrderId = "GPA.3300-1234-5678-90123",
            PurchaseCompletionTimeDateTimeOffset = PurchaseCompletedAt,
            AcknowledgementState = "ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED"
        };
    }
}
