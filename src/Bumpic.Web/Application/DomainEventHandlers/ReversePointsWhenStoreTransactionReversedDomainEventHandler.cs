using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.PointAccount;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 商店交易退款后冲正点数处理器。
/// </summary>
public class ReversePointsWhenStoreTransactionReversedDomainEventHandler(IMediator mediator)
    : IDomainEventHandler<StoreTransactionReversedDomainEvent>
{
    /// <summary>
    /// 通过点数命令完成跨聚合变更。
    /// </summary>
    public async Task Handle(
        StoreTransactionReversedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ReversePurchasedPointsCommand(
            UserAccountId: domainEvent.UserAccountId,
            StoreTransactionId: domainEvent.StoreTransactionId,
            FactKey: domainEvent.FactKey,
            Points: domainEvent.ReversedPoints), cancellationToken);
    }
}
