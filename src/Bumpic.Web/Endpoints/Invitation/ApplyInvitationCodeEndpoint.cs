using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Commands.Invitation;
using Bumpic.Web.Services.Invitations;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.Invitation;

/// <summary>
/// 提交邀请码请求。
/// </summary>
public record ApplyInvitationCodeRequest
{
    /// <summary>
    /// 邀请人分享的邀请码。
    /// </summary>
    [Required]
    public string InvitationCode { get; init; } = string.Empty;

    /// <summary>
    /// 快捷登录自动注册时响应中返回的补交窗口令牌；窗口默认 10 分钟内有效，且只能成功使用一次。
    /// </summary>
    [Required]
    public Guid InvitationWindowToken { get; init; }
}

/// <summary>
/// 提交邀请码响应。
/// </summary>
/// <param name="Established">是否成功建立邀请关系（成功时邀请人获得邀请积分）。</param>
/// <param name="Reason">
/// 未建立时的原因码：INVITATION_CODE_REQUIRED、INVITATION_CODE_INVALID、INVITATION_SELF、INVITATION_ALREADY_BOUND；
/// 成功时为 null。
/// </param>
public record ApplyInvitationCodeResponse(bool Established, string? Reason);

/// <summary>
/// 提交邀请码（需登录）：仅限快捷登录自动注册后的限时窗口内提交，用于补上注册时无法填写的邀请码并给邀请人发放积分。
/// </summary>
[Tags("Invitation")]
[HttpPost("/api/v1/invitation/apply")]
[Authorize(Policy = PolicyNames.Client)]
public class ApplyInvitationCodeEndpoint(
    IMediator mediator,
    ILoginUser loginUser,
    IInvitationWindowStore invitationWindowStore)
    : Endpoint<ApplyInvitationCodeRequest, ResponseData<ApplyInvitationCodeResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(ApplyInvitationCodeRequest request, CancellationToken cancellationToken)
    {
        var userAccountId = new UserAccountId(loginUser.Id);

        // 窗口必须属于当前登录用户，避免拿别人的窗口替他人建立邀请关系。
        var windowOwner = await invitationWindowStore.GetOwnerAsync(request.InvitationWindowToken, cancellationToken);
        if (windowOwner is null || windowOwner.Id != userAccountId.Id)
        {
            throw new KnownException("INVITATION_WINDOW_INVALID");
        }

        var attempts = await invitationWindowStore.RegisterAttemptAsync(request.InvitationWindowToken, cancellationToken);
        if (attempts > InvitationWindowStore.MaxAttempts)
        {
            await invitationWindowStore.CloseAsync(request.InvitationWindowToken, cancellationToken);
            throw new KnownException("INVITATION_WINDOW_LOCKED");
        }

        var result = await mediator.Send(
            new EstablishInvitationCommand(
                InviteeUserAccountId: userAccountId,
                InvitationCode: request.InvitationCode),
            cancellationToken);

        if (result.Established)
        {
            // 建立成功即关闭窗口，保证一次注册只能补交一次邀请码。
            await invitationWindowStore.CloseAsync(request.InvitationWindowToken, cancellationToken);
        }

        await Send.OkAsync(
            new ApplyInvitationCodeResponse(result.Established, result.Reason).AsSuccessResponseData(),
            cancellation: cancellationToken);
    }
}
