using MediatR;
using Microsoft.Extensions.Options;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Points;
using Bumpic.Web.Options;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 用户注册成功后按配置发放注册赠点：业务引用取账户标识，保证同一账户只发放一次；
/// 账户注销后重新注册会得到新的账户标识，因此会重新发放。
/// </summary>
public class UserAccountRegisteredDomainEventHandlerForSendRegistrationPoints(
    IMediator mediator,
    IOptions<RewardPointsOptions> rewardPointsOptions)
    : IDomainEventHandler<UserAccountRegisteredDomainEvent>
{
    /// <summary>
    /// 注册赠点业务引用前缀。
    /// </summary>
    private const string BusinessReferencePrefix = "registration:";

    /// <inheritdoc />
    public async Task Handle(UserAccountRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await mediator.Send(
            new GrantPointsCommand(
                UserAccountId: domainEvent.UserAccount.Id,
                Points: rewardPointsOptions.Value.Registration,
                RecordType: AccountPointRecordType.RegistrationGranted,
                BusinessReference: BusinessReferencePrefix + domainEvent.UserAccount.Id.Id),
            cancellationToken);
    }
}
