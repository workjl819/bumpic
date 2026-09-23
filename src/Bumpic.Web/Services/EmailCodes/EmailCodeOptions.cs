namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 邮箱验证码配置。
/// </summary>
public class EmailCodeOptions
{
    /// <summary>
    /// 验证码位数。
    /// </summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>
    /// 验证码有效时长（秒）。
    /// </summary>
    public int CodeTtlSeconds { get; set; } = 600;

    /// <summary>
    /// 重发冷却时长（秒）。
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// 单个验证码最多验证次数。
    /// </summary>
    public int MaxVerifyAttempts { get; set; } = 5;

    /// <summary>
    /// 限流统计窗口（分钟）。
    /// </summary>
    public int RateLimitWindowMinutes { get; set; } = 15;

    /// <summary>
    /// 限流窗口内允许的发送次数上限。
    /// </summary>
    public int RateLimitMaxRequests { get; set; } = 10;
}
