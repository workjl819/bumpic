using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Queries.PointAccount;
using Bumpic.Web.Extensions;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.PointAccount;

/// <summary>
/// 点数流水分页请求。
/// </summary>
public record GetAccountPointRecordsRequest(int PageIndex=1, int PageSize = 20);

/// <summary>
/// 分页查询当前用户点数流水。
/// </summary>
[HttpPost("/api/v1/account-point-record/pages")]
[Authorize(Policy = PolicyNames.Client)]
[Tags("PointAccount")]
public class GetAccountPointRecordsEndpoint(IMediator mediator, ILoginUser loginUser)
    : Endpoint<GetAccountPointRecordsRequest, ResponseData<PagedData<AccountPointRecordItem>>>
{
    /// <summary>
    /// 返回当前用户的不可变点数流水。
    /// </summary>
    /// <param name="req">分页查询请求。</param>
    /// <param name="ct">取消令牌。</param>
    public override async Task HandleAsync(GetAccountPointRecordsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new GetAccountPointRecordsQuery(
            UserAccountId: new UserAccountId(loginUser.Id),
            PageIndex: req.PageIndex,
            PageSize: req.PageSize), ct);
        await Send.OkAsync(result.AsSuccessResponseData(), cancellation: ct);
    }
}
