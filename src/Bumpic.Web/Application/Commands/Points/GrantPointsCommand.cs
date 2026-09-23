using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using PointAccountEntity = Bumpic.Domain.AggregateModel.PointAccountAggregate.PointAccount;

namespace Bumpic.Web.Application.Commands.Points;

/// <summary>
/// 发放点数命令：只操作点数账户聚合（余额与领域事件），流水由点数已发放事件处理器在独立聚合上追加。
/// </summary>
/// <param name="UserAccountId">目标用户账户标识。</param>
/// <param name="Points">发放点数，必须为正。</param>
/// <param name="RecordType">对应流水类型。</param>
/// <param name="BusinessReference">唯一业务引用，用于幂等。</param>
public record GrantPointsCommand(
    UserAccountId UserAccountId,
    int Points,
    AccountPointRecordType RecordType,
    string BusinessReference) : ICommand;

/// <summary>
/// 发放点数命令验证器。
/// </summary>
public class GrantPointsCommandValidator : AbstractValidator<GrantPointsCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GrantPointsCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
        RuleFor(x => x.Points)
            .GreaterThan(0).WithMessage("发放点数必须大于 0");
        RuleFor(x => x.RecordType)
            .IsInEnum().WithMessage("流水类型无效");
        RuleFor(x => x.BusinessReference)
            .NotEmpty().WithMessage("业务引用不能为空");
    }
}

/// <summary>
/// 发放点数命令处理器：同一业务引用已入账时直接跳过，保证幂等。
/// </summary>
public class GrantPointsCommandHandler(
    IPointAccountRepository pointAccountRepository,
    IAccountPointRecordRepository pointRecordRepository) : ICommandHandler<GrantPointsCommand>
{
    /// <inheritdoc />
    public async Task Handle(GrantPointsCommand request, CancellationToken cancellationToken)
    {
        var alreadyGranted = await pointRecordRepository.ExistsAsync(
            request.RecordType,
            request.BusinessReference,
            cancellationToken);
        if (alreadyGranted)
        {
            return;
        }

        var account = await pointAccountRepository.FindByUserAccountIdAsync(request.UserAccountId, cancellationToken);
        if (account is null)
        {
            account = PointAccountEntity.Create(request.UserAccountId);
            await pointAccountRepository.AddAsync(account, cancellationToken);
        }

        account.Grant(request.Points, request.RecordType, request.BusinessReference);
    }
}
