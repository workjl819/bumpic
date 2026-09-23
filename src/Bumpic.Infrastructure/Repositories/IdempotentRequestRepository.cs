using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 幂等请求仓储接口。
/// </summary>
public interface IIdempotentRequestRepository : IRepository<IdempotentRequest, IdempotentRequestId>
{
    /// <summary>
    /// 按用户、操作和客户端幂等键查找请求。
    /// </summary>
    Task<IdempotentRequest?> FindAsync(
        UserAccountId userAccountId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// 幂等请求仓储实现。
/// </summary>
public class IdempotentRequestRepository(ApplicationDbContext context)
    : RepositoryBase<IdempotentRequest, IdempotentRequestId, ApplicationDbContext>(context),
        IIdempotentRequestRepository
{
    /// <inheritdoc />
    public Task<IdempotentRequest?> FindAsync(
        UserAccountId userAccountId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return context.IdempotentRequests.SingleOrDefaultAsync(
            x => x.UserAccountId == userAccountId
                 && x.Operation == operation
                 && x.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }
}
