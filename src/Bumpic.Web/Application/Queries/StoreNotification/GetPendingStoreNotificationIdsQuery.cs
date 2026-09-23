using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Microsoft.EntityFrameworkCore;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.StoreNotification;

/// <summary>
/// 查询当前可领取的商店通知标识。
/// </summary>
public record GetPendingStoreNotificationIdsQuery(int Take) : IQuery<IReadOnlyList<StoreNotificationReceiptId>>;

/// <summary>
/// 商店通知领取候选查询处理器。
/// </summary>
public class GetPendingStoreNotificationIdsQueryHandler(
    ApplicationDbContext context,
    IClock clock) : IQueryHandler<GetPendingStoreNotificationIdsQuery, IReadOnlyList<StoreNotificationReceiptId>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreNotificationReceiptId>> Handle(
        GetPendingStoreNotificationIdsQuery request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return await context.StoreNotificationReceipts
            .AsNoTracking()
            .Where(x => !x.Deleted
                        && (x.Status == StoreNotificationReceiptStatus.Received
                            || (x.Status == StoreNotificationReceiptStatus.RetryScheduled
                                && (!x.NextRetryAt.HasValue || x.NextRetryAt <= now))
                            || (x.Status == StoreNotificationReceiptStatus.Processing
                                && (!x.LeaseExpiresAt.HasValue || x.LeaseExpiresAt <= now))))
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(request.Take)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// 商店通知领取候选查询验证器。
/// </summary>
public class GetPendingStoreNotificationIdsQueryValidator : AbstractValidator<GetPendingStoreNotificationIdsQuery>
{
    public GetPendingStoreNotificationIdsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}
