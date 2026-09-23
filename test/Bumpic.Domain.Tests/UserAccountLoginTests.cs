using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 用户登录失败锁定领域行为单元测试。
/// </summary>
public class UserAccountLoginTests
{
    /// <summary>
    /// 达到失败阈值后账户被临时锁定。
    /// </summary>
    [Fact]
    public void RecordLoginFailure_ReachesThreshold_LocksAccount()
    {
        var user = UserAccount.Register("lock@example.com", "stored-hash");
        var now = DateTimeOffset.UtcNow;

        user.RecordLoginFailure(maxFailures: 5, lockDuration: TimeSpan.FromMinutes(15), now: now);
        user.RecordLoginFailure(maxFailures: 5, lockDuration: TimeSpan.FromMinutes(15), now: now.AddSeconds(1));
        user.RecordLoginFailure(maxFailures: 5, lockDuration: TimeSpan.FromMinutes(15), now: now.AddSeconds(2));
        user.RecordLoginFailure(maxFailures: 5, lockDuration: TimeSpan.FromMinutes(15), now: now.AddSeconds(3));
        user.RecordLoginFailure(maxFailures: 5, lockDuration: TimeSpan.FromMinutes(15), now: now.AddSeconds(4));

        Assert.True(user.IsLockedOut(now.AddSeconds(5)));
    }

    /// <summary>
    /// 未达阈值前不锁定。
    /// </summary>
    [Fact]
    public void RecordLoginFailure_BelowThreshold_DoesNotLock()
    {
        var user = UserAccount.Register("partial@example.com", "stored-hash");
        var now = DateTimeOffset.UtcNow;

        user.RecordLoginFailure(maxFailures: 5, lockDuration: TimeSpan.FromMinutes(15), now: now);

        Assert.False(user.IsLockedOut(now.AddSeconds(1)));
    }

    /// <summary>
    /// 锁定到期后失败计数重置并重新计数。
    /// </summary>
    [Fact]
    public void RecordLoginFailure_AfterLockExpired_ResetsCounter()
    {
        var user = UserAccount.Register("expire@example.com", "stored-hash");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            user.RecordLoginFailure(5, TimeSpan.FromMinutes(15), now.AddSeconds(i));
        }

        var afterExpiry = now.AddMinutes(16);
        user.RecordLoginFailure(5, TimeSpan.FromMinutes(15), afterExpiry);

        Assert.False(user.IsLockedOut(afterExpiry.AddSeconds(1)));
    }

    /// <summary>
    /// 登录成功后清除失败计数与锁定状态。
    /// </summary>
    [Fact]
    public void ResetLoginFailures_ClearsCountAndLock()
    {
        var user = UserAccount.Register("reset@example.com", "stored-hash");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            user.RecordLoginFailure(5, TimeSpan.FromMinutes(15), now.AddSeconds(i));
        }

        Assert.True(user.IsLockedOut(now.AddSeconds(6)));

        user.ResetLoginFailures(now.AddSeconds(6));

        Assert.False(user.IsLockedOut(now.AddSeconds(7)));
    }
}
