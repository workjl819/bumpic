using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using NetCorePal.Extensions.Dto;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Queries.PointAccount;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.PointAccount;

/// <summary>
/// 查询当前用户点数余额。
/// </summary>
[HttpPost("/api/v1/point-account/balance")]
[Authorize(Policy = PolicyNames.Client)]
[Tags("PointAccount")]
public class GetPointBalanceEndpoint(IMediator mediator, ILoginUser loginUser)
    : EndpointWithoutRequest<ResponseData<GetPointBalanceResult>>
{
    /// <summary>
    /// 返回当前用户可用点数和冻结点数。
    /// </summary>
    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userAccountId = new UserAccountId(loginUser.Id);
        var result = await mediator.Send(
            new GetPointBalanceQuery(UserAccountId: userAccountId),
            cancellationToken);
        await Send.OkAsync(result.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
