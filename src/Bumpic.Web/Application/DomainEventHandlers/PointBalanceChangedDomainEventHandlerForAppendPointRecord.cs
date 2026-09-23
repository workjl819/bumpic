using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Points;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 点数余额变化后通过命令追加独立流水。
/// </summary>
public class PointBalanceChangedDomainEventHandlerForAppendPointRecord(IMediator mediator)
    : IDomainEventHandler<PointBalanceChangedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(
        PointBalanceChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AppendAccountPointRecordCommand(
            PointAccountId: domainEvent.PointAccount.Id,
            UserAccountId: domainEvent.PointAccount.UserAccountId,
            RecordType: domainEvent.RecordType,
            Amount: domainEvent.Amount,
            BusinessReference: domainEvent.BusinessReference), cancellationToken);
    }
}
