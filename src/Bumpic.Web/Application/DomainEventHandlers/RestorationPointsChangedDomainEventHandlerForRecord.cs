using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Points;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 在同一事务追加修复点数审计记录。
/// </summary>
public class RestorationPointsChangedDomainEventHandlerForRecord(IMediator mediator)
    : IDomainEventHandler<RestorationPointsChangedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(RestorationPointsChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // 余额变化后追加同一业务引用的不可变流水，两者随事务一起成功或回滚。
        await mediator.Send(new AppendAccountPointRecordCommand(PointAccountId: domainEvent.PointAccount.Id,
            UserAccountId: domainEvent.PointAccount.UserAccountId, RecordType: domainEvent.RecordType,
            Amount: 5, BusinessReference: domainEvent.BusinessReference), cancellationToken);
    }
}
