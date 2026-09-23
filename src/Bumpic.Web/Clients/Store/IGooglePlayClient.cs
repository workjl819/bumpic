namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Google Play Developer API 验单和消费端口。
/// </summary>
public interface IGooglePlayClient
{
    /// <summary>
    /// 查询购买令牌对应的权威购买状态。
    /// </summary>
    Task<GooglePlayPurchase> GetPurchaseAsync(
        GooglePlayPurchaseRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 服务端消费购买令牌，消费成功后交易才可以入账。
    /// </summary>
    Task ConsumeAsync(
        GooglePlayConsumptionRequest request,
        CancellationToken cancellationToken);
}
