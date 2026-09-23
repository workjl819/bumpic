using StackExchange.Redis;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Web.Services.Invitations;

/// <summary>
/// 快捷登录自动注册后的限时邀请码补交窗口存储。
/// </summary>
public interface IInvitationWindowStore
{
    /// <summary>
    /// 为指定用户开通补交窗口。
    /// </summary>
    /// <param name="userAccountId">新建账户标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>窗口令牌；客户端在窗口内提交邀请码时需要回传。</returns>
    Task<Guid> OpenAsync(UserAccountId userAccountId, CancellationToken cancellationToken);

    /// <summary>
    /// 读取窗口归属用户。
    /// </summary>
    /// <param name="windowToken">窗口令牌。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归属用户标识；窗口不存在或已过期时为 null。</returns>
    Task<UserAccountId?> GetOwnerAsync(Guid windowToken, CancellationToken cancellationToken);

    /// <summary>
    /// 记录一次提交尝试并返回累计次数，用于限制窗口内的枚举尝试。
    /// </summary>
    /// <param name="windowToken">窗口令牌。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>窗口内累计尝试次数。</returns>
    Task<long> RegisterAttemptAsync(Guid windowToken, CancellationToken cancellationToken);

    /// <summary>
    /// 关闭窗口（邀请关系建立成功或尝试次数超限时调用）。
    /// </summary>
    /// <param name="windowToken">窗口令牌。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CloseAsync(Guid windowToken, CancellationToken cancellationToken);
}

/// <summary>
/// 基于 Redis 的补交窗口实现：窗口与尝试次数都带 TTL，过期即失效。
/// </summary>
public class InvitationWindowStore(IConnectionMultiplexer connectionMultiplexer) : IInvitationWindowStore
{
    /// <summary>
    /// 窗口有效期：给用户提交邀请码的时间窗口。
    /// </summary>
    public static readonly TimeSpan WindowTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 窗口内允许的最大提交次数，超过即关闭窗口，避免枚举邀请码。
    /// </summary>
    public const int MaxAttempts = 5;

    private const string KeyPrefix = "Bumpic_invitation_window";

    /// <inheritdoc />
    public async Task<Guid> OpenAsync(UserAccountId userAccountId, CancellationToken cancellationToken)
    {
        var windowToken = Guid.NewGuid();
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(BuildKey(windowToken), userAccountId.Id.ToString(), WindowTtl);
        return windowToken;
    }

    /// <inheritdoc />
    public async Task<UserAccountId?> GetOwnerAsync(Guid windowToken, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(BuildKey(windowToken));
        return value.HasValue && Guid.TryParse(value.ToString(), out var userId)
            ? new UserAccountId(userId)
            : null;
    }

    /// <inheritdoc />
    public async Task<long> RegisterAttemptAsync(Guid windowToken, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var key = BuildAttemptKey(windowToken);
        var attempts = await database.StringIncrementAsync(key);
        if (attempts == 1)
        {
            await database.KeyExpireAsync(key, WindowTtl);
        }

        return attempts;
    }

    /// <inheritdoc />
    public async Task CloseAsync(Guid windowToken, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync([BuildKey(windowToken), BuildAttemptKey(windowToken)]);
    }

    private static string BuildKey(Guid windowToken) => $"{KeyPrefix}:{windowToken:D}";

    private static string BuildAttemptKey(Guid windowToken) => $"{KeyPrefix}:{windowToken:D}:attempts";
}
