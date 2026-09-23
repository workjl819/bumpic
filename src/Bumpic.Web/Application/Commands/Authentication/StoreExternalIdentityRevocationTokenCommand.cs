using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 保存平台撤销令牌密文到外部身份绑定表命令。
/// </summary>
/// <param name="UserAccountId">用户账户标识。</param>
/// <param name="Provider">外部身份提供方。</param>
/// <param name="RevocationTokenCiphertext">加密后的平台撤销令牌。</param>
public record StoreExternalIdentityRevocationTokenCommand(
    UserAccountId UserAccountId,
    UserExternalIdentityProvider Provider,
    string RevocationTokenCiphertext) : ICommand;

/// <summary>
/// 保存平台撤销令牌命令验证器。
/// </summary>
public class StoreExternalIdentityRevocationTokenCommandValidator : AbstractValidator<StoreExternalIdentityRevocationTokenCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public StoreExternalIdentityRevocationTokenCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
        RuleFor(x => x.RevocationTokenCiphertext)
            .NotEmpty().WithMessage("撤销令牌密文不能为空");
    }
}

/// <summary>
/// 保存平台撤销令牌命令处理器；身份未绑定时静默跳过。
/// </summary>
public class StoreExternalIdentityRevocationTokenCommandHandler(IUserExternalIdentityRepository identityRepository)
    : ICommandHandler<StoreExternalIdentityRevocationTokenCommand>
{
    /// <inheritdoc />
    public async Task Handle(StoreExternalIdentityRevocationTokenCommand request, CancellationToken cancellationToken)
    {
        var identity = await identityRepository.FindByUserAndProviderAsync(
            request.UserAccountId,
            request.Provider,
            cancellationToken);
        identity?.SetRevocationToken(request.RevocationTokenCiphertext, DateTimeOffset.UtcNow);
    }
}
