using Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 商店交易事实仓储接口。
/// </summary>
public interface IStoreTransactionFactRepository
    : IRepository<StoreTransactionFact, StoreTransactionFactId>
{
    /// <summary>
    /// 按确定性事实键查询事实。
    /// </summary>
    Task<StoreTransactionFact?> FindByFactKeyAsync(
        AppStore store,
        byte[] factKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// 商店交易事实仓储实现。
/// </summary>
public class StoreTransactionFactRepository(ApplicationDbContext context)
    : RepositoryBase<StoreTransactionFact, StoreTransactionFactId, ApplicationDbContext>(context),
        IStoreTransactionFactRepository
{
    /// <inheritdoc />
    public Task<StoreTransactionFact?> FindByFactKeyAsync(
        AppStore store,
        byte[] factKey,
        CancellationToken cancellationToken)
    {
        return context.StoreTransactionFacts.SingleOrDefaultAsync(
            x => x.Store == store && x.FactKey == factKey,
            cancellationToken);
    }
}
