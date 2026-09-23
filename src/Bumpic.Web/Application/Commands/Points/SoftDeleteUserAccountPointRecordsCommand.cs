using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Points;

/// <summary>
/// 账户注销时软删除该用户的全部点数流水命令。
/// </summary>
/// <param name="UserAccountId">被注销的用户账户标识。</param>
public record SoftDeleteUserAccountPointRecordsCommand(UserAccountId UserAccountId) : ICommand;

/// <summary>
/// 软删除用户点数流水命令验证器。
/// </summary>
public class SoftDeleteUserAccountPointRecordsCommandValidator : AbstractValidator<SoftDeleteUserAccountPointRecordsCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public SoftDeleteUserAccountPointRecordsCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 软删除用户点数流水命令处理器。
/// </summary>
public class SoftDeleteUserAccountPointRecordsCommandHandler(IAccountPointRecordRepository recordRepository)
    : ICommandHandler<SoftDeleteUserAccountPointRecordsCommand>
{
    /// <inheritdoc />
    public async Task Handle(SoftDeleteUserAccountPointRecordsCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var records = await recordRepository.ListByUserAsync(request.UserAccountId, cancellationToken);
        foreach (var record in records)
        {
            record.Delete(now);
        }
    }
}
