namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// Apple 服务端购买补偿结果。
/// </summary>
public enum ReconcileAppleStorePurchaseOutcome
{
    Completed,
    Unlinked,
    RetryableFailed,
    DeterministicFailed
}
