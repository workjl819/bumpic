using MediatR;
using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Invitation;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 账户注销后软删除该用户邀请记录的领域事件处理器。
/// </summary>
public class UserAccountDeletedDomainEventHandlerForSoftDeleteInvitationRecords(IMediator mediator) : IDomainEventHandler<UserAccountDeletedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(UserAccountDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new SoftDeleteUserInvitationRecordsCommand(UserAccountId: domainEvent.UserAccount.Id),
            cancellationToken);
    }
}
