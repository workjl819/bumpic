using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// 商店验单处理结果。
/// </summary>
public record VerifyStorePurchaseCommandResult(
    StorePurchaseVerificationOutcome Outcome,
    string? FailureCode,
    int RetryAfterSeconds,
    StoreTransactionId? StoreTransactionId,
    StoreTransactionStatus? Status,
    string? ProductId,
    int GrantedPoints,
    int ReversedPoints,
    int AvailablePoints,
    int FrozenPoints);
