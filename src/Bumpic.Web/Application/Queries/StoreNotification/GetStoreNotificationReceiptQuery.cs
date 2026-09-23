using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Microsoft.EntityFrameworkCore;
using Bumpic.Infrastructure;

namespace Bumpic.Web.Application.Queries.StoreNotification;

/// <summary>
/// 查询待处理商店通知收件内容。
/// </summary>
public record GetStoreNotificationReceiptQuery(StoreNotificationReceiptId StoreNotificationReceiptId)
    : IQuery<GetStoreNotificationReceiptResult?>;

/// <summary>
/// 商店通知收件处理快照查询处理器。
/// </summary>
public class GetStoreNotificationReceiptQueryHandler(ApplicationDbContext context)
    : IQueryHandler<GetStoreNotificationReceiptQuery, GetStoreNotificationReceiptResult?>
{
    /// <inheritdoc />
    public Task<GetStoreNotificationReceiptResult?> Handle(
        GetStoreNotificationReceiptQuery request,
        CancellationToken cancellationToken)
    {
        return context.StoreNotificationReceipts
            .AsNoTracking()
            .Where(x => x.Id == request.StoreNotificationReceiptId && !x.Deleted)
            .Select(x => x.NormalizedPayload == null
                ? null
                : new GetStoreNotificationReceiptResult(
                    x.Id,
                    x.Store,
                    x.ExternalNotificationId,
                    x.NotificationType,
                    x.NormalizedPayload,
                    x.PayloadHash,
                    x.OccurredAt,
                    x.AttemptCount))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
