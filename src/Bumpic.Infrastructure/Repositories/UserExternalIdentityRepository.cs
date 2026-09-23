using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Infrastructure.Repositories;

/// <summary>
/// 用户外部身份仓储接口。
/// </summary>
public interface IUserExternalIdentityRepository : IRepository<UserExternalIdentity, UserExternalIdentityId>
{
    /// <summary>
    /// 按提供方与主题标识查询未删除身份。
    /// </summary>
    Task<UserExternalIdentity?> FindByProviderAndSubjectAsync(UserExternalIdentityProvider provider, string subjectId, CancellationToken cancellationToken);

    /// <summary>
    /// 按用户与提供方查询未删除身份。
    /// </summary>
    Task<UserExternalIdentity?> FindByUserAndProviderAsync(UserAccountId userAccountId, UserExternalIdentityProvider provider, CancellationToken cancellationToken);

    /// <summary>
    /// 查询用户与提供方对应的身份，包含注销后已软删除的记录。
    /// </summary>
    Task<UserExternalIdentity?> FindByUserAndProviderIncludingDeletedAsync(UserAccountId userAccountId, UserExternalIdentityProvider provider, CancellationToken cancellationToken);

    /// <summary>
    /// 查询用户的全部未删除外部身份。
    /// </summary>
    Task<List<UserExternalIdentity>> ListByUserAsync(UserAccountId userAccountId, CancellationToken cancellationToken);

}

/// <summary>
/// 用户外部身份仓储实现。
/// </summary>
public class UserExternalIdentityRepository(ApplicationDbContext context)
    : RepositoryBase<UserExternalIdentity, UserExternalIdentityId, ApplicationDbContext>(context), IUserExternalIdentityRepository
{
    /// <inheritdoc />
    public Task<UserExternalIdentity?> FindByProviderAndSubjectAsync(UserExternalIdentityProvider provider, string subjectId, CancellationToken cancellationToken)
    {
        return context.UserExternalIdentities.FirstOrDefaultAsync(
            identity => identity.Provider == provider && identity.SubjectId == subjectId && !identity.Deleted,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<UserExternalIdentity?> FindByUserAndProviderAsync(UserAccountId userAccountId, UserExternalIdentityProvider provider, CancellationToken cancellationToken)
    {
        return context.UserExternalIdentities.FirstOrDefaultAsync(
            identity => identity.UserAccountId == userAccountId && identity.Provider == provider && !identity.Deleted,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<UserExternalIdentity?> FindByUserAndProviderIncludingDeletedAsync(UserAccountId userAccountId, UserExternalIdentityProvider provider, CancellationToken cancellationToken)
    {
        return context.UserExternalIdentities
            .Where(identity => identity.UserAccountId == userAccountId && identity.Provider == provider && identity.RevocationTokenCiphertext != string.Empty)
            .OrderByDescending(identity => identity.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<UserExternalIdentity>> ListByUserAsync(UserAccountId userAccountId, CancellationToken cancellationToken)
    {
        return context.UserExternalIdentities
            .Where(identity => identity.UserAccountId == userAccountId && !identity.Deleted)
            .ToListAsync(cancellationToken);
    }

}
