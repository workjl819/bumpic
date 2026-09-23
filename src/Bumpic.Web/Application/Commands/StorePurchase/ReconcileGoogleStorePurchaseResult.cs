using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// Google Play 通知购买对账结果。
/// </summary>
public record ReconcileGoogleStorePurchaseResult(
    ReconcileGoogleStorePurchaseOutcome Outcome,
    StoreTransactionStatus Status,
    string? FailureCode);
