using FastEndpoints;
using Bumpic.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Web.Shared;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Commands.Invitation;

namespace Bumpic.Web.Endpoints.Invitation;

/// <summary>
/// 获取本人邀请码端点。
/// </summary>
[Tags("Invitation")]
[HttpPost("/api/v1/invitation/code")]
[Authorize(Policy = PolicyNames.Client)]
public class GetOwnInvitationCodeEndpoint(IMediator mediator, ILoginUser loginUser)
    : EndpointWithoutRequest<ResponseData<GetOwnInvitationCodeResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = new UserAccountId(loginUser.Id);
        var response = await mediator.Send(new GetOwnInvitationCodeCommand(userId), cancellationToken);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }

}
