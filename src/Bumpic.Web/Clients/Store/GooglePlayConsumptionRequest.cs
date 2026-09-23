namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Google Play 消耗型商品消费请求。
/// </summary>
public record GooglePlayConsumptionRequest(
    string ProductId,
    string PurchaseToken);
