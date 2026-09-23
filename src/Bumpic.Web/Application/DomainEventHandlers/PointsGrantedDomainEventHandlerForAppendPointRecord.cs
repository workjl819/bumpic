using MediatR;
using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Points;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 点数发放后追加流水：流水与点数账户是独立聚合，由事件驱动单独发送 Command。
/// </summary>
public class PointsGrantedDomainEventHandlerForAppendPointRecord(IMediator mediator)
    : IDomainEventHandler<PointsGrantedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(PointsGrantedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new AppendAccountPointRecordCommand(
                PointAccountId: domainEvent.PointAccount.Id,
                UserAccountId: domainEvent.PointAccount.UserAccountId,
                RecordType: domainEvent.RecordType,
                Amount: domainEvent.Amount,
                BusinessReference: domainEvent.BusinessReference),
            cancellationToken);
    }
}