namespace Bumpic.Domain.Enums;

/// <summary>
/// 商店通知收件记录领取结果。
/// </summary>
public enum StoreNotificationReceiptAcquireResult
{
    Acquired,
    Processing,
    RetryNotDue,
    Processed,
    DeadLettered
}
