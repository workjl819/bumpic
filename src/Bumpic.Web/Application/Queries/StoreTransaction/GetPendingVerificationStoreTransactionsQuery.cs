using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.StoreTransaction;

/// <summary>
/// 查询所有等待平台补偿验证的商店交易。
/// </summary>
public record GetPendingVerificationStoreTransactionsQuery
    : IQuery<IReadOnlyList<PendingVerificationStoreTransactionResult>>;

/// <summary>
/// 等待平台补偿验证交易查询处理器。
/// </summary>
public class GetPendingVerificationStoreTransactionsQueryHandler(ApplicationDbContext context)
    : IQueryHandler<GetPendingVerificationStoreTransactionsQuery,
        IReadOnlyList<PendingVerificationStoreTransactionResult>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingVerificationStoreTransactionResult>> Handle(
        GetPendingVerificationStoreTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        return await context.StoreTransactions
            .AsNoTracking()
            .Where(x => !x.Deleted && x.Status == StoreTransactionStatus.PendingVerification)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new PendingVerificationStoreTransactionResult(
                x.Id,
                x.Store,
                x.ExternalTransactionId,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
