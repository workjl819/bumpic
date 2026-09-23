using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;

namespace Bumpic.Web.Application.Commands.StoreNotification;

/// <summary>
/// 商店通知持久化结果。
/// </summary>
public record ReceiveStoreNotificationResult(
    StoreNotificationReceiptId StoreNotificationReceiptId,
    bool Created);
