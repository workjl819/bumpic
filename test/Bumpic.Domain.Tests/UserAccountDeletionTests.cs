using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 账户注销与宽限期领域行为单元测试。
/// </summary>
public class UserAccountDeletionTests
{
    /// <summary>
    /// 提交注销后打上标记，并按宽限期给出计划清除时间。
    /// </summary>
    [Fact]
    public void RequestDeletion_MarksPendingAndSchedulesPurge()
    {
        var user = UserAccount.Register("delete@example.com");
        var now = DateTimeOffset.UtcNow;

        user.RequestDeletion(now, AccountDeletionPolicy.DefaultGracePeriod);

        Assert.True(user.IsDeletionPending);
        Assert.Equal(now, user.DeletionRequestedAt);
        var domainEvent = Assert.Single(user.GetDomainEvents().OfType<AccountDeletionRequestedDomainEvent>());
        Assert.Equal(now.Add(AccountDeletionPolicy.DefaultGracePeriod), domainEvent.ScheduledDeletionAt);
    }

    /// <summary>
    /// 宽限期来自配置：传入 5 分钟时计划清除时间为 5 分钟后。
    /// </summary>
    [Fact]
    public void RequestDeletion_CustomGracePeriod_SchedulesAccordingly()
    {
        var user = UserAccount.Register("custom-grace@example.com");
        var now = DateTimeOffset.UtcNow;
        var gracePeriod = TimeSpan.FromMinutes(5);

        user.RequestDeletion(now, gracePeriod);

        var domainEvent = Assert.Single(user.GetDomainEvents().OfType<AccountDeletionRequestedDomainEvent>());
        Assert.Equal(now.AddMinutes(5), domainEvent.ScheduledDeletionAt);
    }

    /// <summary>
    /// 重复提交注销保持首次申请时间不变，且不重复发布事件。
    /// </summary>
    [Fact]
    public void RequestDeletion_Twice_KeepsFirstRequestTime()
    {
        var user = UserAccount.Register("delete-twice@example.com");
        var now = DateTimeOffset.UtcNow;

        user.RequestDeletion(now, AccountDeletionPolicy.DefaultGracePeriod);
        user.RequestDeletion(now.AddDays(1), AccountDeletionPolicy.DefaultGracePeriod);

        Assert.Equal(now, user.DeletionRequestedAt);
        Assert.Single(user.GetDomainEvents().OfType<AccountDeletionRequestedDomainEvent>());
    }

    /// <summary>
    /// 宽限期内登录会记录最后登录时间并自动取消注销。
    /// </summary>
    [Fact]
    public void RecordLogin_WithinGracePeriod_CancelsDeletion()
    {
        var user = UserAccount.Register("cancel@example.com");
        var now = DateTimeOffset.UtcNow;
        user.RequestDeletion(now, AccountDeletionPolicy.DefaultGracePeriod);

        var cancelled = user.RecordLogin(now.AddDays(7));

        Assert.True(cancelled);
        Assert.False(user.IsDeletionPending);
        Assert.Equal(now.AddDays(7), user.LastLoginAt);
        Assert.Single(user.GetDomainEvents().OfType<AccountDeletionCancelledDomainEvent>());
    }

    /// <summary>
    /// 未提交注销时登录只记录最后登录时间。
    /// </summary>
    [Fact]
    public void RecordLogin_WithoutDeletion_OnlyRecordsLoginTime()
    {
        var user = UserAccount.Register("login@example.com");
        var now = DateTimeOffset.UtcNow;

        var cancelled = user.RecordLogin(now);

        Assert.False(cancelled);
        Assert.Equal(now, user.LastLoginAt);
        Assert.Empty(user.GetDomainEvents().OfType<AccountDeletionCancelledDomainEvent>());
    }

    /// <summary>
    /// 注销执行后账户置为已删除并发布注销事件。
    /// </summary>
    [Fact]
    public void Delete_SetsDeletedStateAndRaisesEvent()
    {
        var user = UserAccount.Register("purge@example.com");
        var now = DateTimeOffset.UtcNow;
        user.RequestDeletion(now.AddDays(-31), AccountDeletionPolicy.DefaultGracePeriod);

        user.Delete(now);

        Assert.True(user.Deleted);
        Assert.Equal(UserAccountStatus.Deleted, user.Status);
        Assert.Equal(now, user.DeletedAt);
        Assert.Single(user.GetDomainEvents().OfType<UserAccountDeletedDomainEvent>());
    }

    /// <summary>
    /// 已注销账户重复执行注销不重复发布事件。
    /// </summary>
    [Fact]
    public void Delete_WhenAlreadyDeleted_IsIdempotent()
    {
        var user = UserAccount.Register("purge-twice@example.com");
        var now = DateTimeOffset.UtcNow;
        user.Delete(now);

        user.Delete(now.AddDays(1));

        Assert.Single(user.GetDomainEvents().OfType<UserAccountDeletedDomainEvent>());
    }

    /// <summary>
    /// 已注销账户不允许提交注销或登录。
    /// </summary>
    [Fact]
    public void RequestDeletion_WhenDeleted_Throws()
    {
        var user = UserAccount.Register("deleted@example.com");
        user.Delete(DateTimeOffset.UtcNow);

        Assert.Throws<KnownException>(() => user.RequestDeletion(DateTimeOffset.UtcNow, AccountDeletionPolicy.DefaultGracePeriod));
        Assert.Throws<KnownException>(() => user.RecordLogin(DateTimeOffset.UtcNow));
    }
}
