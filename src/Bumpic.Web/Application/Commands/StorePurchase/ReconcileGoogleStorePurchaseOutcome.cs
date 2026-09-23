namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// Google Play 通知购买对账结果。
/// </summary>
public enum ReconcileGoogleStorePurchaseOutcome
{
    Completed,
    Unlinked,
    RetryableFailed,
    DeterministicFailed
}
