using FastEndpoints;
using Bumpic.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Shared;
using Bumpic.Web.Application.Queries.Authentication;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 查询当前用户信息（需登录）。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/info")]
[Authorize(Policy = PolicyNames.Client)]
public class GetCurrentUserEndpoint(IMediator mediator, ILoginUser loginUser)
    : EndpointWithoutRequest<ResponseData<CurrentUserInfo>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = new UserAccountId(loginUser.Id);
        var response = await mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }

}
