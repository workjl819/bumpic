namespace Bumpic.Domain.Enums;

/// <summary>
/// 商店交易状态。
/// </summary>
public enum StoreTransactionStatus
{
    PendingVerification,
    PendingConsumption,
    Verified,
    Rejected,
    Canceled,
    Voided,
    Refunded
}
