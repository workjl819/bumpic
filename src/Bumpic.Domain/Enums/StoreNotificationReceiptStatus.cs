namespace Bumpic.Domain.Enums;

/// <summary>
/// 商店通知收件记录处理状态。
/// </summary>
public enum StoreNotificationReceiptStatus
{
    Received,
    Processing,
    RetryScheduled,
    Processed,
    DeadLettered
}
