using Microsoft.Extensions.Options;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Options;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 到期注销账户命令：宽限期（AccountDeletion:GracePeriod）已过的账户软删除自身，并发布注销领域事件驱动后续资源清理与外部解绑。
/// </summary>
/// <param name="UserAccountId">待注销账户标识。</param>
public record PurgeDeletedAccountCommand(UserAccountId UserAccountId) : ICommand;

/// <summary>
/// 到期注销命令验证器。
/// </summary>
public class PurgeDeletedAccountCommandValidator : AbstractValidator<PurgeDeletedAccountCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public PurgeDeletedAccountCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull().Must(id => id.Id != Guid.Empty);
    }
}

/// <summary>
/// 到期注销账户命令锁：按账户串行化，避免用户取消注销与注销执行并发。
/// </summary>
public class PurgeDeletedAccountCommandLock : ICommandLock<PurgeDeletedAccountCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        PurgeDeletedAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings($"account:purge:{command.UserAccountId.Id}"));
    }
}

/// <summary>
/// 到期注销账户命令处理器。
/// </summary>
public class PurgeDeletedAccountCommandHandler(
    IUserAccountRepository userRepository,
    IOptions<AccountDeletionOptions> accountDeletionOptions)
    : ICommandHandler<PurgeDeletedAccountCommand>
{
    /// <inheritdoc />
    public async Task Handle(PurgeDeletedAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.UserAccountId, cancellationToken);
        if (user is null || user.Deleted)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var gracePeriod = accountDeletionOptions.Value.GracePeriod;
        if (user.DeletionRequestedAt is null || user.DeletionRequestedAt.Value.Add(gracePeriod) > now)
        {
            // 宽限期内（或已被用户取消）不执行注销。
            return;
        }

        user.Delete(now);
    }
}
