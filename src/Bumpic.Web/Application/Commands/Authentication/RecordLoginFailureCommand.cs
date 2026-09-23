using Bumpic.Domain;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 记录一次登录失败命令：由 Endpoint 在外部校验（如登录验证码）失败时调用，只维护账户聚合的失败计数与锁定。
/// </summary>
public record RecordLoginFailureCommand(string EmailAddress) : ICommand;

/// <summary>
/// 记录登录失败命令验证器。
/// </summary>
public class RecordLoginFailureCommandValidator : AbstractValidator<RecordLoginFailureCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public RecordLoginFailureCommandValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空");
    }
}

/// <summary>
/// 记录登录失败命令处理器；账户不存在时静默返回，避免泄露账号是否存在。
/// </summary>
public class RecordLoginFailureCommandHandler(IUserAccountRepository userRepository)
    : ICommandHandler<RecordLoginFailureCommand>
{
    /// <inheritdoc />
    public async Task Handle(RecordLoginFailureCommand request, CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);
        var user = await userRepository.FindByEmailAsync(email, cancellationToken);
        if (user is null || user.Deleted)
        {
            return;
        }

        user.RecordLoginFailure(
            maxFailures: UserAccountLoginPolicy.MaxFailures,
            lockDuration: UserAccountLoginPolicy.LockDuration,
            now: DateTimeOffset.UtcNow);
    }
}
