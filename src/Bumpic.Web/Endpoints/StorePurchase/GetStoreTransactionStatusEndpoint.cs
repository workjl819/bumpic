using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using NetCorePal.Extensions.Dto;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Queries.StorePurchase;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.StorePurchase;

/// <summary>
/// 商店交易状态查询请求。
/// </summary>
public record GetStoreTransactionStatusRequest(StoreTransactionId StoreTransactionId);

/// <summary>
/// 查询当前用户自己的商店交易状态。
/// </summary>
[HttpPost("/api/v1/store-transaction/status")]
[Authorize(Policy = PolicyNames.Client)]
[Tags("StorePurchase")]
public class GetStoreTransactionStatusEndpoint(IMediator mediator, ILoginUser loginUser)
    : Endpoint<GetStoreTransactionStatusRequest, ResponseData<GetStoreTransactionStatusResult>>
{
    /// <summary>
    /// 返回交易当前状态和当前余额。
    /// </summary>
    public override async Task HandleAsync(
        GetStoreTransactionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userAccountId = new UserAccountId(loginUser.Id);
        var result = await mediator.Send(new GetStoreTransactionStatusQuery(
            UserAccountId: userAccountId,
            StoreTransactionId: request.StoreTransactionId), cancellationToken);
        await Send.OkAsync(result.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
