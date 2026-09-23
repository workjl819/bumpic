using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.PointAccount;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 商店交易退款撤回后恢复点数处理器。
/// </summary>
public class ReinstatePointsWhenStoreTransactionReinstatedDomainEventHandler(IMediator mediator)
    : IDomainEventHandler<StoreTransactionReinstatedDomainEvent>
{
    /// <summary>
    /// 通过点数命令完成跨聚合变更。
    /// </summary>
    public async Task Handle(
        StoreTransactionReinstatedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ReinstatePurchasedPointsCommand(
            UserAccountId: domainEvent.UserAccountId,
            StoreTransactionId: domainEvent.StoreTransactionId,
            FactKey: domainEvent.FactKey,
            Points: domainEvent.ReinstatedPoints), cancellationToken);
    }
}
