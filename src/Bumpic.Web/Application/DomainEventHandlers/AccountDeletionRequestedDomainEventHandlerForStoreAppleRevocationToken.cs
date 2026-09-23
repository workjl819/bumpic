using MediatR;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Authentication;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 提交注销后把 Apple 撤销令牌密文写入外部身份绑定表，供宽限期届满后撤销授权使用。
/// </summary>
public class AccountDeletionRequestedDomainEventHandlerForStoreAppleRevocationToken(IMediator mediator)
    : IDomainEventHandler<AccountDeletionRequestedDomainEvent>
{
    /// <inheritdoc />
    public async Task Handle(AccountDeletionRequestedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(domainEvent.AppleRevocationTokenCiphertext))
        {
            return;
        }

        await mediator.Send(
            new StoreExternalIdentityRevocationTokenCommand(
                UserAccountId: domainEvent.UserAccount.Id,
                Provider: UserExternalIdentityProvider.Apple,
                RevocationTokenCiphertext: domainEvent.AppleRevocationTokenCiphertext),
            cancellationToken);
    }
}
