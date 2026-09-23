using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Registration;

/// <summary>
/// 邮箱注册命令响应。
/// </summary>
public record RegisterWithEmailResult(string NormalizedEmail);

/// <summary>
/// 邮箱注册命令：创建账户；注册验证码由 Endpoint 预先核销，当前不使用密码。
/// </summary>
public record RegisterWithEmailCommand(
    string EmailAddress) : ICommand<RegisterWithEmailResult>;

/// <summary>
/// 邮箱注册命令验证器。
/// </summary>
public class RegisterWithEmailCommandValidator : AbstractValidator<RegisterWithEmailCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public RegisterWithEmailCommandValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .MaximumLength(320).WithMessage("邮箱长度不能超过 320 个字符");
    }
}

/// <summary>
/// 邮箱注册命令锁：按规范化邮箱串行化，防止并发重复注册。
/// </summary>
public class RegisterWithEmailCommandLock : ICommandLock<RegisterWithEmailCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        RegisterWithEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = EmailAddressNormalizer.Normalize(command.EmailAddress ?? string.Empty);
        return Task.FromResult(new CommandLockSettings($"register:email:{email}"));
    }
}

/// <summary>
/// 邮箱注册命令处理器。
/// </summary>
public class RegisterWithEmailCommandHandler(IUserAccountRepository userRepository)
    : ICommandHandler<RegisterWithEmailCommand, RegisterWithEmailResult>
{
    /// <inheritdoc />
    public async Task<RegisterWithEmailResult> Handle(
        RegisterWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);

        // 聚合级不变式：同一规范化邮箱最多一个未删除账户（Endpoint 已预检，此处为并发兜底）。
        var existing = await userRepository.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new KnownException("EMAIL_ALREADY_REGISTERED");
        }

        var user = UserAccount.Register(emailAddress: email);
        await userRepository.AddAsync(user, cancellationToken);
        return new RegisterWithEmailResult(email);
    }

}
