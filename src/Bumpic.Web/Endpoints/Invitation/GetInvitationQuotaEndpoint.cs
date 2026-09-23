using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Queries.Invitation;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.Invitation;

/// <summary>
/// 邀请配额响应。
/// </summary>
/// <param name="MaxInvitations">每个账号最多可获得邀请奖励的邀请人数。</param>
/// <param name="UsedInvitations">已发放邀请奖励的邀请人数；好友注册并建立邀请关系后才计入。</param>
/// <param name="RemainingInvitations">
/// 还可获得邀请奖励的剩余次数；为 0 时仍可继续邀请（邀请关系照常建立），只是不再发放奖励。
/// </param>
public record InvitationQuotaResponse(
    int MaxInvitations,
    int UsedInvitations,
    int RemainingInvitations);

/// <summary>
/// 查询本人邀请配额（需登录）：返回最多可获得奖励的邀请人数、已发放奖励的邀请人数与剩余奖励次数。
/// 邀请关系本身不设上限：剩余次数为 0 时仍可建立新的邀请关系，只是不再发放奖励。
/// </summary>
[Tags("Invitation")]
[HttpPost("/api/v1/invitation/quota")]
[Authorize(Policy = PolicyNames.Client)]
public class GetInvitationQuotaEndpoint(IMediator mediator, ILoginUser loginUser)
    : EndpointWithoutRequest<ResponseData<InvitationQuotaResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = new UserAccountId(loginUser.Id);
        var granted = await mediator.Send(new GetInviterInvitationCountQuery(userId), cancellationToken);

        var used = granted.InvitationCount;
        var remaining = Math.Max(0, InvitationPolicy.MaxInvitationsPerAccount - used);

        await Send.OkAsync(
            new InvitationQuotaResponse(
                MaxInvitations: InvitationPolicy.MaxInvitationsPerAccount,
                UsedInvitations: used,
                RemainingInvitations: remaining).AsSuccessResponseData(),
            cancellation: cancellationToken);
    }
}
