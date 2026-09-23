using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 邮箱验证码 Redis 存储端口。
/// </summary>
public interface IEmailCodeStore
{
    /// <summary>
    /// 是否处于重发冷却中。
    /// </summary>
    Task<bool> IsInCooldownAsync(string email, OperationType purpose, CancellationToken cancellationToken);

    /// <summary>
    /// 标记重发冷却；已在冷却中时返回 false。
    /// </summary>
    Task<bool> MarkCooldownAsync(string email, OperationType purpose, TimeSpan cooldown, CancellationToken cancellationToken);

    /// <summary>
    /// 是否处于邮箱维度的公共重发冷却中（不区分 purpose，避免切换用途绕过冷却）。
    /// </summary>
    Task<bool> IsInSharedCooldownAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// 标记邮箱维度的公共重发冷却；已在冷却中时返回 false。
    /// </summary>
    Task<bool> MarkSharedCooldownAsync(string email, TimeSpan cooldown, CancellationToken cancellationToken);

    /// <summary>
    /// 尝试登记一次发送并判断是否超出限流窗口；计数达到上限时返回 false。
    /// </summary>
    Task<bool> TryRegisterSendAsync(string email, OperationType purpose, string? ipAddress, string? deviceId, TimeSpan window, int maxRequests, CancellationToken cancellationToken);

    /// <summary>
    /// 保存验证码摘要。
    /// </summary>
    Task SaveCodeAsync(string email, OperationType purpose, string codeDigest, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// 核销验证码：匹配摘要且未超过尝试上限时成功并删除。
    /// </summary>
    Task<bool> TryConsumeCodeAsync(string email, OperationType purpose, string codeDigest, int maxAttempts, CancellationToken cancellationToken);

    /// <summary>
    /// 删除验证码及其计数键。
    /// </summary>
    Task RemoveCodeAsync(string email, OperationType purpose, CancellationToken cancellationToken);
}
