using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// Apple 服务端购买补偿结果。
/// </summary>
public record ReconcileAppleStorePurchaseResult(
    ReconcileAppleStorePurchaseOutcome Outcome,
    StoreTransactionStatus Status,
    string? FailureCode);
