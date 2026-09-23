using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.StoreProduct;

/// <summary>
/// 商店商品查询结果。
/// </summary>
public record GetStoreProductsResult(AppStore Store, IReadOnlyList<StoreProductItem> Items);
