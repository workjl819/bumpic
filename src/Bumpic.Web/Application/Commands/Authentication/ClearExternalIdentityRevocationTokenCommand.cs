using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 清除外部身份绑定表上的平台撤销令牌密文命令（撤销成功后调用）。
/// </summary>
/// <param name="UserAccountId">用户账户标识。</param>
/// <param name="Provider">外部身份提供方。</param>
public record ClearExternalIdentityRevocationTokenCommand(
    UserAccountId UserAccountId,
    UserExternalIdentityProvider Provider) : ICommand;

/// <summary>
/// 清除平台撤销令牌命令验证器。
/// </summary>
public class ClearExternalIdentityRevocationTokenCommandValidator : AbstractValidator<ClearExternalIdentityRevocationTokenCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public ClearExternalIdentityRevocationTokenCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull().Must(id => id.Id != Guid.Empty);
        RuleFor(x => x.Provider).IsInEnum();
    }
}

/// <summary>
/// 清除平台撤销令牌命令处理器；身份不存在时静默跳过。
/// </summary>
public class ClearExternalIdentityRevocationTokenCommandHandler(IUserExternalIdentityRepository identityRepository)
    : ICommandHandler<ClearExternalIdentityRevocationTokenCommand>
{
    /// <inheritdoc />
    public async Task Handle(ClearExternalIdentityRevocationTokenCommand request, CancellationToken cancellationToken)
    {
        var identity = await identityRepository.FindByUserAndProviderIncludingDeletedAsync(
            request.UserAccountId,
            request.Provider,
            cancellationToken);
        identity?.ClearRevocationToken(DateTimeOffset.UtcNow);
    }
}
