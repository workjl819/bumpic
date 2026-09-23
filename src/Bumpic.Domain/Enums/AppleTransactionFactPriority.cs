namespace Bumpic.Domain.Enums;

/// <summary>
/// Apple 同一签名快照时间下的交易事实优先级。
/// </summary>
public enum AppleTransactionFactPriority
{
    /// <summary>
    /// 购买事实。
    /// </summary>
    Purchased = 1,

    /// <summary>
    /// 取消事实。
    /// </summary>
    Canceled = 2,

    /// <summary>
    /// 退款或作废事实。
    /// </summary>
    RefundedOrVoided = 3,

    /// <summary>
    /// 退款撤回事实。
    /// </summary>
    RefundReversed = 4
}
