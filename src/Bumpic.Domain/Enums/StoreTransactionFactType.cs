namespace Bumpic.Domain.Enums;

/// <summary>
/// 商店交易事实类型。
/// </summary>
public enum StoreTransactionFactType
{
    Purchased,
    Canceled,
    Voided,
    Refunded,
    RefundReversed
}
