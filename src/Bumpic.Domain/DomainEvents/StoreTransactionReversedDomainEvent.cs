using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 商店交易退款差额已确认领域事件。
/// </summary>
public record StoreTransactionReversedDomainEvent(
    StoreTransactionId StoreTransactionId,
    UserAccountId UserAccountId,
    string FactKey,
    int ReversedPoints) : IDomainEvent;
