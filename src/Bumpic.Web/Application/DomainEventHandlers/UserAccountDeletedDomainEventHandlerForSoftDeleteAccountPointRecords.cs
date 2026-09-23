using MediatR;
using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Points;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 账户注销后软删除该用户点数流水的领域事件处理器。
/// </summary>
public class UserAccountDeletedDomainEventHandlerForSoftDeleteAccountPointRecords(IMediator mediator) : IDomainEventHandler<UserAccountDeletedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(UserAccountDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new SoftDeleteUserAccountPointRecordsCommand(UserAccountId: domainEvent.UserAccount.Id),
            cancellationToken);
    }
}
