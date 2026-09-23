using Microsoft.Extensions.Options;
using Bumpic.Domain;

namespace Bumpic.Web.Options;

/// <summary>
/// 账户注销配置。
/// </summary>
public class AccountDeletionOptions
{
    /// <summary>
    /// 注销宽限期：提交注销到实际清除数据之间的时长，期间账户可正常使用、登录即自动取消注销。
    /// 配置节 <c>AccountDeletion:GracePeriod</c>，支持 <c>30.00:00:00</c>、<c>00:05:00</c> 这类 TimeSpan 写法。
    /// </summary>
    public TimeSpan GracePeriod { get; set; } = AccountDeletionPolicy.DefaultGracePeriod;
}

/// <summary>
/// 账户注销配置验证器。
/// </summary>
public class AccountDeletionOptionsValidator : IValidateOptions<AccountDeletionOptions>
{
    /// <summary>
    /// 校验宽限期取值。
    /// </summary>
    /// <param name="name">配置名称。</param>
    /// <param name="options">待验证配置。</param>
    public ValidateOptionsResult Validate(string? name, AccountDeletionOptions options)
    {
        if (options.GracePeriod <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("AccountDeletion:GracePeriod 必须大于 0。");
        }

        if (options.GracePeriod > TimeSpan.FromDays(365))
        {
            return ValidateOptionsResult.Fail("AccountDeletion:GracePeriod 不能超过 365 天。");
        }

        return ValidateOptionsResult.Success;
    }
}
