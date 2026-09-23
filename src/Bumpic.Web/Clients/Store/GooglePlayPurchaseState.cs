namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Google Play 购买状态。
/// </summary>
public enum GooglePlayPurchaseState
{
    /// <summary>
    /// 已支付，等待消费。
    /// </summary>
    Purchased,

    /// <summary>
    /// 延迟付款仍待完成。
    /// </summary>
    Pending,

    /// <summary>
    /// 延迟付款已取消。
    /// </summary>
    Canceled
}
