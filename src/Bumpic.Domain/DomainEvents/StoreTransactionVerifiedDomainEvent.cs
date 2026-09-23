using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 商店交易点数已可发放领域事件。
/// </summary>
public record StoreTransactionVerifiedDomainEvent(
    StoreTransactionId StoreTransactionId,
    UserAccountId UserAccountId,
    int GrantedPoints) : IDomainEvent;
