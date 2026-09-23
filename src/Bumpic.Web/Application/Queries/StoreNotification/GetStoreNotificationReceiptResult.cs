using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.StoreNotification;

/// <summary>
/// 商店通知收件处理快照。
/// </summary>
public record GetStoreNotificationReceiptResult(
    StoreNotificationReceiptId StoreNotificationReceiptId,
    AppStore Store,
    string ExternalNotificationId,
    string NotificationType,
    string NormalizedPayload,
    byte[] PayloadHash,
    DateTimeOffset OccurredAt,
    int AttemptCount);
