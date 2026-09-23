using Bumpic.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bumpic.Web.Application.Queries.StoreProduct;

/// <summary>
/// 查询当前可售商店商品。
/// </summary>
public record GetStoreProductsQuery(AppStore Store) : IQuery<GetStoreProductsResult>;

/// <summary>
/// 商店商品查询处理器。
/// </summary>
public class GetStoreProductsQueryHandler(ApplicationDbContext dbContext)
    : IQueryHandler<GetStoreProductsQuery, GetStoreProductsResult>
{
    /// <summary>
    /// 查询当前商店已启用的商品。
    /// </summary>
    public async Task<GetStoreProductsResult> Handle(
        GetStoreProductsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.StoreProducts
            .AsNoTracking()
            .Where(x => x.Store == request.Store
                        && x.Enabled
                        && !x.Deleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ProductId)
            .Select(x => new StoreProductItem(
                ProductId: x.ProductId,
                Points: x.Points,
                Amount: x.Amount,
                CurrencyCode: x.CurrencyCode,
                SortOrder: x.SortOrder))
            .ToListAsync(cancellationToken);

        return new GetStoreProductsResult(
            Store: request.Store,
            Items: items);
    }
}

/// <summary>
/// 商店商品查询验证器。
/// </summary>
public class GetStoreProductsQueryValidator : AbstractValidator<GetStoreProductsQuery>
{
    /// <summary>
    /// 初始化查询验证规则。
    /// </summary>
    public GetStoreProductsQueryValidator()
    {
        RuleFor(x => x.Store).IsInEnum();
    }
}
