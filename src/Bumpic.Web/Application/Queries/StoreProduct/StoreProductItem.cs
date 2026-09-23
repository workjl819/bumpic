namespace Bumpic.Web.Application.Queries.StoreProduct;

/// <summary>
/// 商店商品展示项。
/// </summary>
public record StoreProductItem(
    string ProductId,
    int Points,
    decimal Amount,
    string CurrencyCode,
    int SortOrder);
