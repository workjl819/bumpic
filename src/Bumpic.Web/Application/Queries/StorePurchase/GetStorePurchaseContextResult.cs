using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.StorePurchase;

/// <summary>
/// 商店购买上下文查询结果。
/// </summary>
public record GetStorePurchaseContextResult(
    UserAccountId UserAccountId,
    AppStore Store,
    Guid PurchaseAccountToken);
