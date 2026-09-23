using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 商店通知收件记录仓储接口。
/// </summary>
public interface IStoreNotificationReceiptRepository
    : IRepository<StoreNotificationReceipt, StoreNotificationReceiptId>
{
    /// <summary>
    /// 按平台通知身份查询收件记录。
    /// </summary>
    Task<StoreNotificationReceipt?> FindAsync(
        AppStore store,
        string externalNotificationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 商店通知收件记录仓储实现。
/// </summary>
public class StoreNotificationReceiptRepository(ApplicationDbContext context)
    : RepositoryBase<StoreNotificationReceipt, StoreNotificationReceiptId, ApplicationDbContext>(context),
        IStoreNotificationReceiptRepository
{
    /// <inheritdoc />
    public Task<StoreNotificationReceipt?> FindAsync(
        AppStore store,
        string externalNotificationId,
        CancellationToken cancellationToken)
    {
        return context.StoreNotificationReceipts.SingleOrDefaultAsync(
            x => x.Store == store && x.ExternalNotificationId == externalNotificationId,
            cancellationToken);
    }
}
