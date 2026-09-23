using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.AggregateModel.PointAccountAggregate;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 点数账户仓储接口。
/// </summary>
public interface IPointAccountRepository : IRepository<PointAccount, PointAccountId>
{
    /// <summary>
    /// 按用户标识获取点数账户及其流水。
    /// </summary>
    Task<PointAccount?> GetByUserAccountIdAsync(
        Bumpic.Domain.AggregateModel.UserAccountAggregate.UserAccountId userAccountId,
        CancellationToken cancellationToken);
    /// <summary>
    /// 按用户账户查询未删除的点数账户。
    /// </summary>
    Task<PointAccount?> FindByUserAccountIdAsync(UserAccountId userAccountId, CancellationToken cancellationToken);
}

/// <summary>
/// 点数账户仓储实现。
/// </summary>
public class PointAccountRepository(ApplicationDbContext context)
    : RepositoryBase<PointAccount, PointAccountId, ApplicationDbContext>(context), IPointAccountRepository
{
    /// <inheritdoc />
    public Task<PointAccount?> GetByUserAccountIdAsync(
        Bumpic.Domain.AggregateModel.UserAccountAggregate.UserAccountId userAccountId,
        CancellationToken cancellationToken)
    {
        return context.PointAccounts.SingleOrDefaultAsync(
            x => x.UserAccountId == userAccountId && !x.Deleted,
            cancellationToken);
    }
    /// <inheritdoc />
    public Task<PointAccount?> FindByUserAccountIdAsync(UserAccountId userAccountId, CancellationToken cancellationToken)
    {
        // 领域事件可能在首次 SaveChanges 前追加流水，此时新账户仍在 EF 跟踪集合中。
        var trackedAccount = context.PointAccounts.Local.FirstOrDefault(
            account => account.UserAccountId == userAccountId && !account.Deleted);
        if (trackedAccount is not null)
        {
            return Task.FromResult<PointAccount?>(trackedAccount);
        }

        return context.PointAccounts.FirstOrDefaultAsync(
            account => account.UserAccountId == userAccountId && !account.Deleted,
            cancellationToken);
    }
}
