using Bumpic.Web.Services.SessionTokens;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 注销当前会话命令。
/// </summary>
public record LogoutCommand(string RefreshToken) : ICommand;

/// <summary>
/// 注销当前会话命令验证器。
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("刷新令牌不能为空");
    }
}

/// <summary>
/// 注销当前会话命令处理器。
/// </summary>
public class LogoutCommandHandler(SessionTokenIssuer sessionTokenIssuer) : ICommandHandler<LogoutCommand>
{
    /// <inheritdoc />
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        return sessionTokenIssuer.RevokeAsync(request.RefreshToken, cancellationToken);
    }
}
