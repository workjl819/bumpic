using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Points;

/// <summary>
/// 账户注销时软删除该用户的点数账户命令；未消费点数随之作废，不退款不折现。
/// </summary>
/// <param name="UserAccountId">被注销的用户账户标识。</param>
public record SoftDeleteUserPointAccountCommand(UserAccountId UserAccountId) : ICommand;

/// <summary>
/// 软删除用户点数账户命令验证器。
/// </summary>
public class SoftDeleteUserPointAccountCommandValidator : AbstractValidator<SoftDeleteUserPointAccountCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public SoftDeleteUserPointAccountCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 软删除用户点数账户命令处理器。
/// </summary>
public class SoftDeleteUserPointAccountCommandHandler(IPointAccountRepository pointAccountRepository)
    : ICommandHandler<SoftDeleteUserPointAccountCommand>
{
    /// <inheritdoc />
    public async Task Handle(SoftDeleteUserPointAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await pointAccountRepository.FindByUserAccountIdAsync(request.UserAccountId, cancellationToken);
        account?.Delete(DateTimeOffset.UtcNow);
    }
}
