using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.UserAccountAggregate;

/// <summary>
/// 用户账户标识。
/// </summary>
public partial record UserAccountId : IGuidStronglyTypedId;

/// <summary>
/// 用户账户聚合根。
/// </summary>
public class UserAccount : Entity<UserAccountId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected UserAccount()
    {
    }

    /// <summary>
    /// 注册并创建用户账户；邀请码在账户创建时自动生成。
    /// 当前注册流程仅校验邮箱验证码、不使用密码，密码摘要参数保留以便后续扩展。
    /// </summary>
    /// <param name="emailAddress">规范化后的登录邮箱。</param>
    /// <param name="passwordHash">可选的安全哈希后的登录密码（单列存储文本）；不传时为空字符串。</param>
    /// <param name="submittedInvitationCode">注册时提交的可选邀请码，用于建立邀请关系。</param>
    /// <returns>新建的用户账户。</returns>
    public static UserAccount Register(string emailAddress, string? passwordHash = null, string? submittedInvitationCode = null)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            throw new KnownException("EMAIL_REQUIRED");
        }

        var now = DateTimeOffset.UtcNow;
        var userAccount = new UserAccount
        {
            EmailAddress = emailAddress,
            PasswordHash = passwordHash ?? string.Empty,
            InvitationCode = InvitationCodeGenerator.Generate(),
            PurchaseAccountToken = Guid.NewGuid(),
            Status = UserAccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            // 注册即登录：注册成功时同时记录首次登录时间。
            LastLoginAt = now
        };
        userAccount.AddDomainEvent(new UserAccountRegisteredDomainEvent(userAccount, submittedInvitationCode));
        return userAccount;
    }

    /// <summary>
    /// 确保账户持有邀请码；为空时补生成一个邀请码。
    /// </summary>
    public void EnsureInvitationCode()
    {
        if (string.IsNullOrWhiteSpace(InvitationCode))
        {
            InvitationCode = InvitationCodeGenerator.Generate();
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 判断账户当前是否处于安全锁定状态。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <returns>锁定中返回 true。</returns>
    public bool IsLockedOut(DateTimeOffset now)
    {
        return LockedUntil > now;
    }

    /// <summary>
    /// 记录一次登录失败；达到阈值时触发临时锁定。
    /// </summary>
    /// <param name="maxFailures">触发锁定的最大失败次数。</param>
    /// <param name="lockDuration">锁定持续时长。</param>
    /// <param name="now">当前时间。</param>
    public void RecordLoginFailure(int maxFailures, TimeSpan lockDuration, DateTimeOffset now)
    {
        if (IsLockedOut(now))
        {
            return;
        }

        if (LockedUntil != DateTimeOffset.MinValue && LockedUntil <= now)
        {
            FailedLoginCount = 0;
            LockedUntil = DateTimeOffset.MinValue;
        }

        FailedLoginCount += 1;
        if (FailedLoginCount >= maxFailures)
        {
            LockedUntil = now.Add(lockDuration);
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// 登录成功后清除失败计数与锁定状态。
    /// </summary>
    /// <param name="now">当前时间。</param>
    public void ResetLoginFailures(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockedUntil = DateTimeOffset.MinValue;
        UpdatedAt = now;
    }

    /// <summary>
    /// 记录一次成功登录；账户处于注销宽限期内时同时取消注销。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <returns>本次登录是否取消了注销。</returns>
    /// <exception cref="KnownException"><c>ACCOUNT_DELETED</c>：账户已注销。</exception>
    public bool RecordLogin(DateTimeOffset now)
    {
        if (Deleted)
        {
            throw new KnownException("ACCOUNT_DELETED");
        }

        LastLoginAt = now;
        UpdatedAt = now;

        if (DeletionRequestedAt is null)
        {
            return false;
        }

        // 宽限期内重新登录即自动取消注销。
        DeletionRequestedAt = null;
        AddDomainEvent(new AccountDeletionCancelledDomainEvent(this));
        return true;
    }

    /// <summary>
    /// 提交注销申请；重复提交保持首次申请时间不变。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <param name="gracePeriod">注销宽限期，由配置项 <c>AccountDeletion:GracePeriod</c> 提供。</param>
    /// <param name="appleRevocationTokenCiphertext">Apple 撤销令牌密文（可选，由 Endpoint 在事务外换取并加密）。</param>
    /// <exception cref="KnownException"><c>ACCOUNT_DELETED</c>：账户已注销。</exception>
    public void RequestDeletion(DateTimeOffset now, TimeSpan gracePeriod, string? appleRevocationTokenCiphertext = null)
    {
        if (Deleted)
        {
            throw new KnownException("ACCOUNT_DELETED");
        }

        if (DeletionRequestedAt is not null)
        {
            return;
        }

        DeletionRequestedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new AccountDeletionRequestedDomainEvent(
            this,
            now.Add(gracePeriod),
            appleRevocationTokenCiphertext));
    }

    /// <summary>
    /// 宽限期到期后软删除账户本身；外部身份解绑由 <c>UserAccountDeleted</c> 事件处理器完成。
    /// </summary>
    /// <param name="now">当前时间。</param>
    public void Delete(DateTimeOffset now)
    {
        if (Deleted)
        {
            return;
        }

        Status = UserAccountStatus.Deleted;
        Deleted = true;
        DeletedAt = now;
        UpdatedAt = now;
        AddDomainEvent(new UserAccountDeletedDomainEvent(this));
    }

    /// <summary>
    /// 最近一次成功登录时间；从未登录时为 null。定时注销扫描与活跃度判断使用。
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>
    /// 注销申请时间；为 null 表示未提交注销。宽限期内账户仍可正常使用，登录即自动取消。
    /// </summary>
    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    /// <summary>
    /// 是否处于注销宽限期内。
    /// </summary>
    public bool IsDeletionPending => DeletionRequestedAt is not null;

    /// <summary>
    /// 规范化后的登录邮箱。
    /// </summary>
    public string EmailAddress { get; private set; } = string.Empty;

    /// <summary>
    /// 安全哈希后的登录密码；当前注册与登录均不使用密码，字段保留以便后续扩展。
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// 长期有效的邀请码。
    /// </summary>
    public string InvitationCode { get; private set; } = string.Empty;

    /// <summary>
    /// 向 Apple 和 Google 提供的稳定、非业务主键购买账户标识。
    /// </summary>
    public Guid PurchaseAccountToken { get; private set; } = Guid.Empty;

    /// <summary>
    /// 用户账户状态。
    /// </summary>
    public UserAccountStatus Status { get; private set; } = UserAccountStatus.Active;

    /// <summary>
    /// 连续登录失败次数。
    /// </summary>
    public int FailedLoginCount { get; private set; } = 0;

    /// <summary>
    /// 安全锁定截止时间。
    /// </summary>
    public DateTimeOffset LockedUntil { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 账户创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 账户最近更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 软删除标记。
    /// </summary>
    public bool Deleted { get; private set; } = false;

    /// <summary>
    /// 乐观并发控制版本。
    /// </summary>
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 账户删除时间。
    /// </summary>
    public DateTimeOffset DeletedAt { get; private set; } = DateTimeOffset.MinValue;
}
