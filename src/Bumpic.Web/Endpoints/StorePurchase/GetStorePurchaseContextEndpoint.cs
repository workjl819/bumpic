using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using NetCorePal.Extensions.Dto;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Queries.StorePurchase;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.StorePurchase;

/// <summary>
/// 商店购买上下文请求。
/// </summary>
public record GetStorePurchaseContextRequest(AppStore Store);

/// <summary>
/// 商店购买上下文响应。
/// </summary>
public record GetStorePurchaseContextResponse(
    AppStore Store,
    string? AppAccountToken,
    string? ObfuscatedAccountId);

/// <summary>
/// 获取当前用户的商店购买账户参数。
/// </summary>
[HttpPost("/api/v1/store-purchase/context")]
[Authorize(Policy = PolicyNames.Client)]
[Tags("StorePurchase")]
public class GetStorePurchaseContextEndpoint(IMediator mediator, ILoginUser loginUser)
    : Endpoint<GetStorePurchaseContextRequest, ResponseData<GetStorePurchaseContextResponse>>
{
    /// <summary>
    /// 读取稳定的购买账户参数。
    /// </summary>
    public override async Task HandleAsync(
        GetStorePurchaseContextRequest request,
        CancellationToken cancellationToken)
    {
        var context = await mediator.Send(new GetStorePurchaseContextQuery(
            UserAccountId: new UserAccountId(loginUser.Id),
            Store: request.Store), cancellationToken);
        var response = new GetStorePurchaseContextResponse(
            Store: context.Store,
            AppAccountToken: context.Store == AppStore.AppleAppStore
                ? context.PurchaseAccountToken.ToString("D")
                : null,
            ObfuscatedAccountId: context.Store == AppStore.GooglePlay
                ? context.PurchaseAccountToken.ToString("D")
                : null);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
