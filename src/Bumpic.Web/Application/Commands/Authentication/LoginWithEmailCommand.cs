using Bumpic.Domain;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 邮箱登录命令响应。
/// </summary>
public record LoginWithEmailResult(string NormalizedEmail);

/// <summary>
/// 邮箱登录命令：校验账号存在与锁定状态并维护失败计数；登录验证码由 Endpoint 预先核销，当前不使用密码。
/// </summary>
public record LoginWithEmailCommand(string EmailAddress) : ICommand<LoginWithEmailResult>;

/// <summary>
/// 邮箱登录命令验证器。
/// </summary>
public class LoginWithEmailCommandValidator : AbstractValidator<LoginWithEmailCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public LoginWithEmailCommandValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确");
    }
}

/// <summary>
/// 邮箱登录命令处理器。
/// </summary>
public class LoginWithEmailCommandHandler(IUserAccountRepository userRepository)
    : ICommandHandler<LoginWithEmailCommand, LoginWithEmailResult>
{
    /// <inheritdoc />
    public async Task<LoginWithEmailResult> Handle(
        LoginWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);
        var user = await userRepository.FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            throw new KnownException("INVALID_CREDENTIALS");
        }

        var now = DateTimeOffset.UtcNow;
        if (user.IsLockedOut(now))
        {
            throw new KnownException("ACCOUNT_LOCKED");
        }

        user.ResetLoginFailures(now);

        // 记录最后登录时间；若处于注销宽限期内，同时自动取消注销。
        user.RecordLogin(now);
        return new LoginWithEmailResult(email);
    }
}