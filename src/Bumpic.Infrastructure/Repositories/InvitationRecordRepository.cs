using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 邀请记录仓储接口。
/// </summary>
public interface IInvitationRecordRepository : IRepository<InvitationRecord, InvitationRecordId>
{
    /// <summary>
    /// 按受邀用户查询其已建立的未删除邀请关系。
    /// </summary>
    Task<InvitationRecord?> FindByInviteeAsync(UserAccountId inviteeUserAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// 查询与该用户相关的全部未删除邀请记录（作为邀请人或受邀用户），供账户注销时批量软删除。
    /// </summary>
    Task<List<InvitationRecord>> ListByInvolvedUserAsync(UserAccountId userAccountId, CancellationToken cancellationToken);
}

/// <summary>
/// 邀请记录仓储实现。
/// </summary>
public class InvitationRecordRepository(ApplicationDbContext context)
    : RepositoryBase<InvitationRecord, InvitationRecordId, ApplicationDbContext>(context), IInvitationRecordRepository
{
    /// <inheritdoc />
    public Task<InvitationRecord?> FindByInviteeAsync(UserAccountId inviteeUserAccountId, CancellationToken cancellationToken)
    {
        return context.InvitationRecords.FirstOrDefaultAsync(record => record.InviteeUserAccountId == inviteeUserAccountId && !record.Deleted, cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<InvitationRecord>> ListByInvolvedUserAsync(UserAccountId userAccountId, CancellationToken cancellationToken)
    {
        return context.InvitationRecords
            .Where(record => !record.Deleted
                && (record.InviterUserAccountId == userAccountId || record.InviteeUserAccountId == userAccountId))
            .ToListAsync(cancellationToken);
    }
}