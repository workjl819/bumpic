using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.StoreTransaction;

/// <summary>
/// 查询已到补偿时间的 Google Play 待消费交易。
/// </summary>
public record GetDuePendingConsumptionStoreTransactionsQuery(int Take)
    : IQuery<IReadOnlyList<PendingConsumptionStoreTransactionResult>>;

/// <summary>
/// 已到补偿时间的 Google Play 待消费交易查询处理器。
/// </summary>
public class GetDuePendingConsumptionStoreTransactionsQueryHandler(
    ApplicationDbContext context,
    IClock clock) : IQueryHandler<GetDuePendingConsumptionStoreTransactionsQuery,
    IReadOnlyList<PendingConsumptionStoreTransactionResult>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingConsumptionStoreTransactionResult>> Handle(
        GetDuePendingConsumptionStoreTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return await context.StoreTransactions
            .AsNoTracking()
            .Where(x => !x.Deleted
                        && x.Store == AppStore.GooglePlay
                        && x.Status == StoreTransactionStatus.PendingConsumption
                        && (!x.NextRetryAt.HasValue || x.NextRetryAt <= now)
                        && x.ProductId != null)
            .OrderBy(x => x.NextRetryAt ?? x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new PendingConsumptionStoreTransactionResult(
                x.Id,
                x.ExternalTransactionId,
                x.ProductId!,
                x.ConsumptionAttemptCount,
                x.CreatedAt))
            .Take(request.Take)
            .ToListAsync(cancellationToken);
    }
}
