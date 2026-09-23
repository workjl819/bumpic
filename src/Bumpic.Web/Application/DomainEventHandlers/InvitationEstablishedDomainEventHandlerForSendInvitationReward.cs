using MediatR;
using Bumpic.Domain;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Points;
using Bumpic.Web.Application.Queries.Invitation;

namespace Bumpic.Web.Application.DomainEventHandlers;

/// <summary>
/// 邀请关系建立后向邀请人发放邀请奖励；受邀者不发放邀请积分（仅保留其注册赠点）。
/// </summary>
/// <remarks>
/// 邀请人数上限只限制奖励：邀请人已达上限时**仍保留邀请关系**，仅本次不再发放奖励（不抛异常，
/// 因此不会回滚受邀者的注册或邀请关系）。计数按邀请人的邀请奖励流水（<c>InvitationGranted</c>）进行。
/// </remarks>
public class InvitationEstablishedDomainEventHandlerForSendInvitationReward(
    IMediator mediator,
    ILogger<InvitationEstablishedDomainEventHandlerForSendInvitationReward> logger)
    : IDomainEventHandler<InvitationEstablishedDomainEvent>
{
    /// <summary>
    /// 邀请奖励业务引用前缀。
    /// </summary>
    private const string BusinessReferencePrefix = "invitation:";

    /// <inheritdoc />
    public async Task Handle(InvitationEstablishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var inviterUserAccountId = domainEvent.InvitationRecord.InviterUserAccountId;

        var grantedInvitationCount = await mediator.Send(
            new GetInviterInvitationCountQuery(inviterUserAccountId),
            cancellationToken);
        if (grantedInvitationCount.InvitationCount >= InvitationPolicy.MaxInvitationsPerAccount)
        {
            logger.LogInformation(
                "邀请人已达邀请奖励上限（{MaxInvitations} 人），本次仅建立邀请关系、不发放奖励：邀请人 {InviterUserAccountId}",
                InvitationPolicy.MaxInvitationsPerAccount,
                inviterUserAccountId.Id);
            return;
        }

        await mediator.Send(
            new GrantPointsCommand(
                UserAccountId: inviterUserAccountId,
                Points: domainEvent.InvitationRecord.RewardPoints,
                RecordType: AccountPointRecordType.InvitationGranted,
                BusinessReference: BusinessReferencePrefix + domainEvent.InvitationRecord.Id.Id),
            cancellationToken);
    }
}
