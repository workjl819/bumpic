using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Invitation;

/// <summary>
/// 账户注销时软删除与该用户相关的全部邀请记录命令（作为邀请人或受邀用户）。
/// </summary>
/// <param name="UserAccountId">被注销的用户账户标识。</param>
public record SoftDeleteUserInvitationRecordsCommand(UserAccountId UserAccountId) : ICommand;

/// <summary>
/// 软删除用户邀请记录命令验证器。
/// </summary>
public class SoftDeleteUserInvitationRecordsCommandValidator : AbstractValidator<SoftDeleteUserInvitationRecordsCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public SoftDeleteUserInvitationRecordsCommandValidator()
    {
        RuleFor(x => x.UserAccountId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 软删除用户邀请记录命令处理器。
/// </summary>
public class SoftDeleteUserInvitationRecordsCommandHandler(IInvitationRecordRepository invitationRecordRepository)
    : ICommandHandler<SoftDeleteUserInvitationRecordsCommand>
{
    /// <inheritdoc />
    public async Task Handle(SoftDeleteUserInvitationRecordsCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var records = await invitationRecordRepository.ListByInvolvedUserAsync(request.UserAccountId, cancellationToken);
        foreach (var record in records)
        {
            record.Delete(now);
        }
    }
}
