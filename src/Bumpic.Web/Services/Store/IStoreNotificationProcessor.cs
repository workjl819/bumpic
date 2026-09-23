using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;

namespace Bumpic.Web.Services.Store;

/// <summary>
/// durable inbox 商店通知业务处理器。
/// </summary>
public interface IStoreNotificationProcessor
{
    Task ProcessAsync(StoreNotificationReceiptId receiptId, CancellationToken cancellationToken);
}
