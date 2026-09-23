using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.PointAccount;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 商店交易入账后发放点数处理器。
/// </summary>
public class GrantPointsWhenStoreTransactionVerifiedDomainEventHandler(IMediator mediator)
    : IDomainEventHandler<StoreTransactionVerifiedDomainEvent>
{
    /// <summary>
    /// 通过点数命令完成跨聚合变更。
    /// </summary>
    public async Task Handle(
        StoreTransactionVerifiedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new GrantPurchasedPointsCommand(
            UserAccountId: domainEvent.UserAccountId,
            StoreTransactionId: domainEvent.StoreTransactionId,
            Points: domainEvent.GrantedPoints), cancellationToken);
    }
}
