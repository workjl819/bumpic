using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using NetCorePal.Extensions.Dto;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Queries.StoreProduct;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.StoreProduct;

/// <summary>
/// 商店商品查询请求。
/// </summary>
public record GetStoreProductsRequest(AppStore Store);

/// <summary>
/// 查询当前支持购买的点数商品。
/// </summary>
[HttpPost("/api/v1/store-product/pages")]
[Authorize(Policy = PolicyNames.Client)]
[Tags("StoreProduct")]
public class GetStoreProductsEndpoint(IMediator mediator)
    : Endpoint<GetStoreProductsRequest, ResponseData<GetStoreProductsResult>>
{
    /// <summary>
    /// 返回当前商店可售商品。
    /// </summary>
    public override async Task HandleAsync(GetStoreProductsRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStoreProductsQuery(
            Store: request.Store), cancellationToken);
        await Send.OkAsync(result.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
