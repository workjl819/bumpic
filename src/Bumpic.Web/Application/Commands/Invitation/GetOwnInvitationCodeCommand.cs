using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.Invitation;

/// <summary>
/// 获取本人邀请码命令响应。
/// </summary>
/// <param name="InvitationCode">本人长期有效的邀请码，可直接用于分享。</param>
public record GetOwnInvitationCodeResponse(string InvitationCode);

/// <summary>
/// 获取本人邀请码命令；账户缺少邀请码时补生成。
/// </summary>
public record GetOwnInvitationCodeCommand(UserAccountId UserId) : ICommand<GetOwnInvitationCodeResponse>;

/// <summary>
/// 获取本人邀请码命令验证器。
/// </summary>
public class GetOwnInvitationCodeCommandValidator : AbstractValidator<GetOwnInvitationCodeCommand>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public GetOwnInvitationCodeCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotNull().WithMessage("用户标识不能为空")
            .Must(userId => userId.Id != Guid.Empty).WithMessage("用户标识不能为空");
    }
}

/// <summary>
/// 获取本人邀请码命令处理器。
/// </summary>
public class GetOwnInvitationCodeCommandHandler(IUserAccountRepository userRepository)
    : ICommandHandler<GetOwnInvitationCodeCommand, GetOwnInvitationCodeResponse>
{
    /// <inheritdoc />
    public async Task<GetOwnInvitationCodeResponse> Handle(
        GetOwnInvitationCodeCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.UserId, cancellationToken);
        if (user is null || user.Deleted)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        user.EnsureInvitationCode();
        return new GetOwnInvitationCodeResponse(user.InvitationCode);
    }
}