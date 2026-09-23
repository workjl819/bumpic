namespace Bumpic.Web.Services.Store;

/// <summary>
/// Google RTDN 中定位一次性商品交易所需的规范化数据。
/// </summary>
internal sealed record GooglePlayNotificationData(
    string PurchaseToken,
    string? ProductId,
    string? OrderId);
