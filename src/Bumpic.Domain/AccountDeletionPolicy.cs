namespace Bumpic.Domain;

/// <summary>
/// 账户注销策略。
/// </summary>
public static class AccountDeletionPolicy
{
    /// <summary>
    /// 编译期默认宽限期（30 天）：提交注销到实际清除数据之间，账户可正常使用，用户登录即自动取消注销。
    /// 运行时取值由配置项 <c>AccountDeletion:GracePeriod</c> 提供（开发环境可调短以便验证）。
    /// </summary>
    public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromDays(30);
}
