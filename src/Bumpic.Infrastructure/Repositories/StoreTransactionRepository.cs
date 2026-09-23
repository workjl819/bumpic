using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 商店交易仓储接口。
/// </summary>
public interface IStoreTransactionRepository : IRepository<StoreTransaction, StoreTransactionId>
{
    /// <summary>
    /// 按商店和平台原始交易标识查找交易。
    /// </summary>
    Task<StoreTransaction?> FindByExternalTransactionIdAsync(
        AppStore store,
        string externalTransactionId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 商店交易仓储实现。
/// </summary>
public class StoreTransactionRepository(ApplicationDbContext context)
    : RepositoryBase<StoreTransaction, StoreTransactionId, ApplicationDbContext>(context), IStoreTransactionRepository
{
    /// <inheritdoc />
    public Task<StoreTransaction?> FindByExternalTransactionIdAsync(
        AppStore store,
        string externalTransactionId,
        CancellationToken cancellationToken)
    {
        return context.StoreTransactions.SingleOrDefaultAsync(
            x => x.Store == store && x.ExternalTransactionId == externalTransactionId,
            cancellationToken);
    }
}
