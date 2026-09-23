using MediatR;
using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.Commands.Authentication;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 账户注销后软删除该用户外部身份绑定的领域事件处理器。
/// </summary>
public class UserAccountDeletedDomainEventHandlerForSoftDeleteExternalIdentities(IMediator mediator) : IDomainEventHandler<UserAccountDeletedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(UserAccountDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new SoftDeleteUserExternalIdentitiesCommand(UserAccountId: domainEvent.UserAccount.Id),
            cancellationToken);
    }
}
