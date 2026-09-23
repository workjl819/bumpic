using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Points;

/// <summary>
/// 在任务事务中维护修复点数。
/// </summary>
/// <param name="UserAccountId">任务所属用户。</param>
/// <param name="RecordType">点数操作。</param>
/// <param name="BusinessReference">任务唯一引用。</param>
public record ApplyRestorationPointsCommand(UserAccountId UserAccountId,
    AccountPointRecordType RecordType, string BusinessReference) : ICommand;

/// <summary>
/// 修复点数命令验证器。
/// </summary>
public class ApplyRestorationPointsCommandValidator : AbstractValidator<ApplyRestorationPointsCommand>
{
    /// <summary>
    /// 配置修复点数输入约束。
    /// </summary>
    public ApplyRestorationPointsCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotNull().NotEqual(new UserAccountId(Id: Guid.Empty));
        RuleFor(x => x.BusinessReference).NotEmpty().MaximumLength(255);
        RuleFor(x => x.RecordType).Must(type => type is AccountPointRecordType.RestorationFrozen
            or AccountPointRecordType.RestorationSettled or AccountPointRecordType.RestorationReleased);
    }
}

/// <summary>
/// 修复点数处理器，余额变化后通过领域事件追加流水。
/// </summary>
public class ApplyRestorationPointsCommandHandler(IPointAccountRepository accounts,
    IAccountPointRecordRepository records) : ICommandHandler<ApplyRestorationPointsCommand>
{
    /// <inheritdoc />
    public async Task Handle(ApplyRestorationPointsCommand request, CancellationToken cancellationToken)
    {
        // 修复点数只能作用于任务所属账户，账户不存在时不创建空账户规避余额检查。
        var account = await accounts.FindByUserAccountIdAsync(request.UserAccountId, cancellationToken)
            ?? throw new KnownException("INSUFFICIENT_POINTS");
        // 不可变流水是该业务引用已执行的事实，重复消息不再改变余额。
        if (await records.ExistsAsync(request.RecordType, request.BusinessReference, cancellationToken))
        {
            return;
        }
        account.ApplyRestoration(userAccountId: request.UserAccountId,
            recordType: request.RecordType, businessReference: request.BusinessReference);
    }
}
