using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.StorePurchase;

/// <summary>
/// 商店交易当前状态和余额。
/// </summary>
public record GetStoreTransactionStatusResult(
    StoreTransactionId StoreTransactionId,
    AppStore Store,
    string? ProductId,
    StoreTransactionStatus Status,
    int GrantedPoints,
    int ReversedPoints,
    string? FailureCode,
    int AvailablePoints,
    int FrozenPoints,
    DateTimeOffset UpdatedAt);
