using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 用户账户仓储接口。
/// </summary>
public interface IUserAccountRepository : IRepository<UserAccount, UserAccountId>
{
    /// <summary>
    /// 按规范化邮箱查询未删除账户。
    /// </summary>
    Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// 按邀请码查询未删除账户。
    /// </summary>
    Task<UserAccount?> FindByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken);

    /// <summary>
    /// 按购买账户标识查询账户，包括软删除账户的历史映射。
    /// </summary>
    Task<UserAccount?> FindByPurchaseAccountTokenAsync(Guid purchaseAccountToken, CancellationToken cancellationToken);
}

/// <summary>
/// 用户账户仓储实现。
/// </summary>
public class UserAccountRepository(ApplicationDbContext context)
    : RepositoryBase<UserAccount, UserAccountId, ApplicationDbContext>(context), IUserAccountRepository
{
    /// <inheritdoc />
    public Task<UserAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return context.UserAccounts.FirstOrDefaultAsync(account => account.EmailAddress == normalizedEmail && !account.Deleted, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UserAccount?> FindByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken)
    {
        return context.UserAccounts.FirstOrDefaultAsync(account => account.InvitationCode == invitationCode && !account.Deleted, cancellationToken);
    }

    /// <inheritdoc />
    public Task<UserAccount?> FindByPurchaseAccountTokenAsync(Guid purchaseAccountToken, CancellationToken cancellationToken)
    {
        return context.UserAccounts.SingleOrDefaultAsync(
            account => account.PurchaseAccountToken == purchaseAccountToken,
            cancellationToken);
    }
}
