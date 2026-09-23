using MediatR;
using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Invitation;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 用户注册成功后，若提交了邀请码则建立唯一邀请关系（同一工作单元内）。
/// </summary>
public class UserAccountRegisteredDomainEventHandlerForInvite(IMediator mediator)
    : IDomainEventHandler<UserAccountRegisteredDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(
        UserAccountRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(domainEvent.SubmittedInvitationCode))
        {
            return;
        }

        await mediator.Send(
            new EstablishInvitationCommand(
                InviteeUserAccountId: domainEvent.UserAccount.Id,
                InvitationCode: domainEvent.SubmittedInvitationCode),
            cancellationToken);
    }
}