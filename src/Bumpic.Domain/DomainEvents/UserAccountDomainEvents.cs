using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 用户账户已注册领域事件。
/// </summary>
public record UserAccountRegisteredDomainEvent(UserAccount UserAccount, string? SubmittedInvitationCode = null) : IDomainEvent;

/// <summary>
/// 用户已提交注销申请领域事件；宽限期内账户仍可正常使用。
/// </summary>
/// <param name="UserAccount">提交注销的账户。</param>
/// <param name="ScheduledDeletionAt">计划清除数据的时间。</param>
/// <param name="AppleRevocationTokenCiphertext">
/// Apple 撤销令牌密文（可选）；由账户注销处理器写入外部身份绑定表，供宽限期届满后撤销授权使用。
/// </param>
public record AccountDeletionRequestedDomainEvent(
    UserAccount UserAccount,
    DateTimeOffset ScheduledDeletionAt,
    string? AppleRevocationTokenCiphertext = null) : IDomainEvent;

/// <summary>
/// 用户重新登录后自动取消注销领域事件。
/// </summary>
/// <param name="UserAccount">取消注销的账户。</param>
public record AccountDeletionCancelledDomainEvent(UserAccount UserAccount) : IDomainEvent;

/// <summary>
/// 宽限期到期、账户及其相关资源进入清除流程的领域事件。
/// </summary>
/// <param name="UserAccount">被注销的账户。</param>
public record UserAccountDeletedDomainEvent(UserAccount UserAccount) : IDomainEvent;
