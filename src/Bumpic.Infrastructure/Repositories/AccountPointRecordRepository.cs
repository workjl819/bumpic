using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 账户点数流水仓储接口。
/// </summary>
public interface IAccountPointRecordRepository : IRepository<AccountPointRecord, AccountPointRecordId>
{
    /// <summary>
    /// 判断指定类型与业务引用是否已存在流水（幂等判定）。
    /// </summary>
    Task<bool> ExistsAsync(AccountPointRecordType recordType, string businessReference, CancellationToken cancellationToken);

    /// <summary>
    /// 查询用户全部未删除点数流水，供账户注销时批量软删除。
    /// </summary>
    Task<List<AccountPointRecord>> ListByUserAsync(UserAccountId userAccountId, CancellationToken cancellationToken);
}

/// <summary>
/// 账户点数流水仓储实现。
/// </summary>
public class AccountPointRecordRepository(ApplicationDbContext context)
    : RepositoryBase<AccountPointRecord, AccountPointRecordId, ApplicationDbContext>(context), IAccountPointRecordRepository
{
    /// <inheritdoc />
    public Task<bool> ExistsAsync(AccountPointRecordType recordType, string businessReference, CancellationToken cancellationToken)
    {
        return context.AccountPointRecords.AnyAsync(
            record => record.Type == recordType && record.BusinessReference == businessReference,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<AccountPointRecord>> ListByUserAsync(UserAccountId userAccountId, CancellationToken cancellationToken)
    {
        return context.AccountPointRecords
            .Where(record => record.UserAccountId == userAccountId && !record.Deleted)
            .ToListAsync(cancellationToken);
    }
}
