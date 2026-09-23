using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Authentication;

/// <summary>
/// 账户注销时软删除该用户的全部外部身份绑定命令；调用 Apple / Google 注销接口撤销授权由集成事件完成。
/// </summary>
/// <param name="UserAccountId">被注销的用户账户标识。</param>
public record SoftDeleteUserExternalIdentitiesCommand(UserAccountId UserAccountId) : ICommand;

/// <summary>
/// 软删除用户外部身份绑定命令验证器。
/// </summary>
public class SoftDeleteUserExternalIdentitiesCommandValidator : AbstractValidator<SoftDeleteUserExternalIdentitiesCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public SoftDeleteUserExternalIdentitiesCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 软删除用户外部身份绑定命令处理器。
/// </summary>
public class SoftDeleteUserExternalIdentitiesCommandHandler(IUserExternalIdentityRepository identityRepository)
    : ICommandHandler<SoftDeleteUserExternalIdentitiesCommand>
{
    /// <inheritdoc />
    public async Task Handle(SoftDeleteUserExternalIdentitiesCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var identities = await identityRepository.ListByUserAsync(request.UserAccountId, cancellationToken);
        foreach (var identity in identities)
        {
            identity.Delete(now);
        }
    }
}
