using Microsoft.Extensions.Options;
using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Options;

namespace Bumpic.Web.Application.Commands.Invitation;

/// <summary>
/// 建立邀请关系命令结果。
/// </summary>
/// <param name="Established">是否成功建立邀请关系（并触发邀请人奖励）。</param>
/// <param name="Reason">未建立时的原因码；成功时为 null。</param>
public record EstablishInvitationResult(bool Established, string? Reason);

/// <summary>
/// 建立邀请关系命令：受邀用户注册成功后（或快捷登录注册的限时补交窗口内）按邀请码建立唯一关系。
/// </summary>
/// <param name="InviteeUserAccountId">受邀用户账户标识。</param>
/// <param name="InvitationCode">邀请码。</param>
public record EstablishInvitationCommand(
    UserAccountId InviteeUserAccountId,
    string? InvitationCode) : ICommand<EstablishInvitationResult>;

/// <summary>
/// 建立邀请关系命令验证器。
/// </summary>
public class EstablishInvitationCommandValidator : AbstractValidator<EstablishInvitationCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public EstablishInvitationCommandValidator()
    {
        RuleFor(x => x.InviteeUserAccountId)
            .NotNull().WithMessage("受邀用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("受邀用户标识不能为空");
        RuleFor(x => x.InvitationCode)
            .MaximumLength(64).WithMessage("邀请码长度不能超过 64 个字符");
    }
}

/// <summary>
/// 建立邀请关系命令处理器：邀请码无效、自邀或受邀者已有关系时不建立关系，并把原因返回给调用方。
/// 邀请人数上限只限制奖励发放（由 InvitationEstablished 处理器判断），不影响邀请关系建立与受邀者注册。
/// </summary>
public class EstablishInvitationCommandHandler(
    IUserAccountRepository userRepository,
    IInvitationRecordRepository invitationRecordRepository,
    IOptions<RewardPointsOptions> rewardPointsOptions) : ICommandHandler<EstablishInvitationCommand, EstablishInvitationResult>
{
    /// <inheritdoc />
    public async Task<EstablishInvitationResult> Handle(
        EstablishInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvitationCode))
        {
            return new EstablishInvitationResult(false, "INVITATION_CODE_REQUIRED");
        }

        var invitationCode = request.InvitationCode.Trim().ToUpperInvariant();
        var inviter = await userRepository.FindByInvitationCodeAsync(invitationCode, cancellationToken);
        if (inviter is null)
        {
            return new EstablishInvitationResult(false, "INVITATION_CODE_INVALID");
        }

        if (inviter.Id == request.InviteeUserAccountId)
        {
            return new EstablishInvitationResult(false, "INVITATION_SELF");
        }

        var existing = await invitationRecordRepository.FindByInviteeAsync(request.InviteeUserAccountId, cancellationToken);
        if (existing is not null)
        {
            return new EstablishInvitationResult(false, "INVITATION_ALREADY_BOUND");
        }

        // 奖励点数取配置 RewardPoints:Invitation（默认 5 点），并写入邀请记录；后续发放直接使用记录上的值。
        var record = InvitationRecord.Establish(
            inviterUserAccountId: inviter.Id,
            inviteeUserAccountId: request.InviteeUserAccountId,
            invitationCode: invitationCode,
            rewardPoints: rewardPointsOptions.Value.Invitation);
        await invitationRecordRepository.AddAsync(record, cancellationToken);

        // 邀请人奖励与邀请人数上限由 InvitationEstablished 的领域事件处理器负责；达到上限只跳过奖励发放，邀请关系仍然成立。
        return new EstablishInvitationResult(true, null);
    }
}
