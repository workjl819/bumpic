namespace Bumpic.Domain.Enums;

/// <summary>
/// 点数流水类型。
/// </summary>
public enum AccountPointRecordType
{
    /// <summary>
    /// 用户注册后发放点数。
    /// </summary>
    RegistrationGranted = 0,

    /// <summary>
    /// 邀请关系建立后发放点数。
    /// </summary>
    InvitationGranted = 1,

    /// <summary>
    /// 用户购买后发放点数。
    /// </summary>
    PurchaseGranted = 2,

    /// <summary>
    /// 创建修复任务时冻结点数。
    /// </summary>
    RestorationFrozen = 3,

    /// <summary>
    /// 修复任务成功后结算冻结点数。
    /// </summary>
    RestorationSettled = 4,

    /// <summary>
    /// 修复任务未成功时释放冻结点数。
    /// </summary>
    RestorationReleased = 5,

    /// <summary>
    /// 补偿用户时发放点数。
    /// </summary>
    CompensationGranted = 6,

    /// <summary>
    /// 购买交易退款后冲正已发放点数。
    /// </summary>
    PurchaseReversed = 7,

    /// <summary>
    /// 购买交易退款撤回后恢复已冲正点数。
    /// </summary>
    PurchaseReinstated = 8
}
