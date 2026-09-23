namespace Bumpic.Domain;

/// <summary>
/// 用户登录失败锁定策略。
/// </summary>
public static class UserAccountLoginPolicy
{
    /// <summary>
    /// 触发临时锁定的最大连续失败次数（密码或验证码失败均计入）。
    /// </summary>
    public const int MaxFailures = 5;

    /// <summary>
    /// 触发锁定后的持续时长。
    /// </summary>
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
}
