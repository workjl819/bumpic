using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 商店交易退款已撤回领域事件。
/// </summary>
public record StoreTransactionReinstatedDomainEvent(
    StoreTransactionId StoreTransactionId,
    UserAccountId UserAccountId,
    string FactKey,
    int ReinstatedPoints) : IDomainEvent;
