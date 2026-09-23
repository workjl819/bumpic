using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Points;

/// <summary>
/// 追加点数流水命令：只操作流水记录聚合（独立表），由点数已发放事件驱动。
/// </summary>
/// <param name="PointAccountId">逻辑关联的点数账户标识。</param>
/// <param name="UserAccountId">冗余保存的用户账户标识。</param>
/// <param name="RecordType">流水类型。</param>
/// <param name="Amount">本次点数变化值，不能为零。</param>
/// <param name="BusinessReference">唯一业务引用。</param>
public record AppendAccountPointRecordCommand(
    PointAccountId PointAccountId,
    UserAccountId UserAccountId,
    AccountPointRecordType RecordType,
    int Amount,
    string BusinessReference) : ICommand;

/// <summary>
/// 追加点数流水命令验证器。
/// </summary>
public class AppendAccountPointRecordCommandValidator : AbstractValidator<AppendAccountPointRecordCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public AppendAccountPointRecordCommandValidator()
    {
        RuleFor(x => x.PointAccountId)
            .NotNull().WithMessage("点数账户标识不能为空")
            .Must(accountId => accountId.Id != Guid.Empty).WithMessage("点数账户标识不能为空");
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
        RuleFor(x => x.RecordType)
            .IsInEnum().WithMessage("流水类型无效");
        RuleFor(x => x.Amount)
            .NotEqual(0).WithMessage("流水金额不能为 0");
        RuleFor(x => x.BusinessReference)
            .NotEmpty().WithMessage("业务引用不能为空");
    }
}

/// <summary>
/// 追加点数流水命令处理器：同一业务引用已存在时跳过，保证幂等。
/// </summary>
public class AppendAccountPointRecordCommandHandler(IAccountPointRecordRepository pointRecordRepository,
    IPointAccountRepository pointAccountRepository)
    : ICommandHandler<AppendAccountPointRecordCommand>
{
    /// <inheritdoc />
    public async Task Handle(AppendAccountPointRecordCommand request, CancellationToken cancellationToken)
    {
        // 流水冗余的用户标识必须与点数账户的实际归属一致。
        var account = await pointAccountRepository.FindByUserAccountIdAsync(request.UserAccountId, cancellationToken);
        if (account is null || account.Id != request.PointAccountId)
        {
            throw new KnownException("POINT_ACCOUNT_USER_MISMATCH");
        }
        // 先查询唯一业务引用，避免重复事件追加第二条流水。
        var exists = await pointRecordRepository.ExistsAsync(
            request.RecordType,
            request.BusinessReference,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var record = AccountPointRecord.Create(
            pointAccountId: request.PointAccountId,
            userAccountId: request.UserAccountId,
            recordType: request.RecordType,
            amount: request.Amount,
            businessReference: request.BusinessReference);
        await pointRecordRepository.AddAsync(record, cancellationToken);
    }
}
