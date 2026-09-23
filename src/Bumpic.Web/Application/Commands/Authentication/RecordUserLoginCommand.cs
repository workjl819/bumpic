using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 记录用户登录命令：更新最后登录时间，并在注销宽限期内自动取消注销。
/// </summary>
/// <param name="UserAccountId">用户账户标识。</param>
public record RecordUserLoginCommand(UserAccountId UserAccountId) : ICommand;

/// <summary>
/// 与到期注销使用同一账户锁，保证登录取消注销与删除扫描串行执行。
/// </summary>
public class RecordUserLoginCommandLock : ICommandLock<RecordUserLoginCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(RecordUserLoginCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings($"account:purge:{command.UserAccountId.Id}"));
    }
}

/// <summary>
/// 记录用户登录命令验证器。
/// </summary>
public class RecordUserLoginCommandValidator : AbstractValidator<RecordUserLoginCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public RecordUserLoginCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 记录用户登录命令处理器。
/// </summary>
public class RecordUserLoginCommandHandler(IUserAccountRepository userRepository)
    : ICommandHandler<RecordUserLoginCommand>
{
    /// <inheritdoc />
    public async Task Handle(RecordUserLoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.UserAccountId, cancellationToken);
        if (user is null || user.Deleted)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        user.ResetLoginFailures(DateTimeOffset.UtcNow);
        user.RecordLogin(DateTimeOffset.UtcNow);
    }
}
