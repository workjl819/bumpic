using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 商店商品仓储接口。
/// </summary>
public interface IStoreProductRepository : IRepository<StoreProduct, StoreProductId>
{
    /// <summary>
    /// 按商店和商店商品标识查找白名单商品。
    /// </summary>
    Task<StoreProduct?> FindByProductIdAsync(
        AppStore store,
        string productId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 商店商品仓储实现。
/// </summary>
public class StoreProductRepository(ApplicationDbContext context)
    : RepositoryBase<StoreProduct, StoreProductId, ApplicationDbContext>(context), IStoreProductRepository
{
    /// <inheritdoc />
    public Task<StoreProduct?> FindByProductIdAsync(
        AppStore store,
        string productId,
        CancellationToken cancellationToken)
    {
        return context.StoreProducts.SingleOrDefaultAsync(
            x => x.Store == store && x.ProductId == productId && !x.Deleted,
            cancellationToken);
    }
}
