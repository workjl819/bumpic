using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Services.SessionTokens;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 刷新令牌命令响应。
/// </summary>
public record RefreshUserTokenResult(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// 轮换刷新令牌命令。
/// </summary>
public record RefreshUserTokenCommand(string RefreshToken) : ICommand<RefreshUserTokenResult>;

/// <summary>
/// 轮换刷新令牌命令验证器。
/// </summary>
public class RefreshUserTokenCommandValidator : AbstractValidator<RefreshUserTokenCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public RefreshUserTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("刷新令牌不能为空");
    }
}

/// <summary>
/// 轮换刷新令牌命令处理器。
/// </summary>
public class RefreshUserTokenCommandHandler(
    SessionTokenIssuer sessionTokenIssuer,
    IUserAccountRepository userRepository) : ICommandHandler<RefreshUserTokenCommand, RefreshUserTokenResult>
{
    /// <inheritdoc />
    public async Task<RefreshUserTokenResult> Handle(
        RefreshUserTokenCommand request,
        CancellationToken cancellationToken)
    {
        var refresh = await sessionTokenIssuer.RefreshAsync(request.RefreshToken, cancellationToken);
        var user = await userRepository.GetAsync(refresh.UserId, cancellationToken);
        if (user is null || user.Deleted)
        {
            await sessionTokenIssuer.RevokeAsync(refresh.SessionTokens.RefreshToken, cancellationToken);
            throw new KnownException("USER_NOT_FOUND");
        }

        return new RefreshUserTokenResult(
            AccessToken: refresh.SessionTokens.AccessToken,
            RefreshToken: refresh.SessionTokens.RefreshToken,
            ExpiresInSeconds: refresh.SessionTokens.ExpiresInSeconds);
    }
}
