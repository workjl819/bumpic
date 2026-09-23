using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.Commands.Registration;
using Bumpic.Web.Application.Queries.Authentication;
using Bumpic.Web.Application.Queries.Registration;
using Bumpic.Web.Services.ExternalIdentities;
using Bumpic.Web.Services.SessionTokens;
using Bumpic.Web.Services.Invitations;
using Bumpic.Web.Shared;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// Apple 快捷登录请求。
/// </summary>
public record LoginWithAppleExternalIdentityRequest
{
    /// <summary>
    /// Sign in with Apple 返回的 identityToken（JWS，勿加 "Bearer " 前缀）。
    /// </summary>
    [Required]
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Apple 授权码（可选，服务端暂未消费）。
    /// </summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>
    /// 客户端自己生成、并交给 Apple SDK 的原始 nonce；客户端应在 SDK 侧使用它的 SHA-256 摘要。
    /// 服务端同时接受「原文」与「SHA-256 十六进制摘要」两种写法，因此传原始 nonce 即可。
    /// </summary>
    public string? Nonce { get; init; }}

/// <summary>
/// Apple 快捷登录请求验证器：令牌缺失或传 null 时先返回参数校验错误，不进入外部身份校验逻辑。
/// </summary>
public class LoginWithAppleExternalIdentityRequestValidator : Validator<LoginWithAppleExternalIdentityRequest>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public LoginWithAppleExternalIdentityRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("identityToken 不能为空")
            .MaximumLength(8192).WithMessage("identityToken 长度非法");
        RuleFor(x => x.Nonce)
            .MaximumLength(256).WithMessage("nonce 长度不能超过 256 个字符");
        RuleFor(x => x.AuthorizationCode)
            .MaximumLength(4096).WithMessage("authorizationCode 长度非法");
    }
}

/// <summary>
/// Apple 快捷登录与注册端点（匿名，单一入口，前端不需要预先判断是否已绑定）。
/// </summary>
/// <remarks>
/// 平台身份已绑定：直接登录，返回 Action=LoggedIn；
/// 平台身份未绑定：信任令牌中已验证的邮箱（email_verified=true），该邮箱已注册时绑定到该账户并返回 Action=Bound，
/// 该邮箱未注册时先注册账户再绑定并返回 Action=Registered（与邮箱注册效果一致，注册赠点等后续动作相同）；
/// 令牌未包含已验证邮箱（例如 Apple 仅在首次授权返回邮箱）时返回 EXTERNAL_IDENTITY_EMAIL_REQUIRED，
/// 前端应改为邮箱验证码登录后携带登录令牌再次调用本接口完成绑定。
/// </remarks>
[Tags("User")]
[HttpPost("/api/v1/user/external-identity/apple/login")]
[AllowAnonymous]
public class LoginWithAppleExternalIdentityEndpoint(
    IMediator mediator,
    SessionTokenIssuer sessionTokenIssuer,
    AppleExternalIdentityVerifier appleVerifier,
    IInvitationWindowStore invitationWindowStore,
    ILogger<LoginWithAppleExternalIdentityEndpoint> logger)
    : Endpoint<LoginWithAppleExternalIdentityRequest, ResponseData<ExternalIdentityAuthenticationResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(LoginWithAppleExternalIdentityRequest request, CancellationToken cancellationToken)
    {
        var principal = await appleVerifier.VerifyAsync(request.Token, request.Nonce, cancellationToken);

        var binding = await mediator.Send(
            new GetExternalIdentityBindingQuery(UserExternalIdentityProvider.Apple, principal.SubjectId),
            cancellationToken);

        GetUserAccountByEmailResponse account;
        ExternalIdentityAuthenticationAction action;
        if (binding.IsBound)
        {
            account = await mediator.Send(new GetUserAccountByEmailQuery(binding.EmailAddress), cancellationToken);
            action = ExternalIdentityAuthenticationAction.LoggedIn;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(principal.VerifiedEmail))
            {
                // 令牌没有已验证邮箱，无法定位或创建邮箱账户：改由邮箱验证码登录后绑定。
                logger.LogWarning("快捷登录缺少已验证邮箱，返回 EXTERNAL_IDENTITY_EMAIL_REQUIRED：平台身份未绑定且令牌无可用邮箱");
                throw new KnownException("EXTERNAL_IDENTITY_EMAIL_REQUIRED");
            }

            account = await mediator.Send(new GetUserAccountByEmailQuery(principal.VerifiedEmail), cancellationToken);
            if (account.UserId is null)
            {
                action = ExternalIdentityAuthenticationAction.Registered;
                try
                {
                    // 复用邮箱注册命令：注册赠点等后续动作与邮箱注册完全一致。
                    await mediator.Send(new RegisterWithEmailCommand(EmailAddress: principal.VerifiedEmail), cancellationToken);
                }
                catch (KnownException exception) when (exception.Message == "EMAIL_ALREADY_REGISTERED")
                {
                    // 并发首次登录时另一请求已注册成功，继续按已有账户绑定。
                }

                account = await mediator.Send(new GetUserAccountByEmailQuery(principal.VerifiedEmail), cancellationToken);
            }
            else
            {
                action = ExternalIdentityAuthenticationAction.Bound;
            }

            if (account.UserId is null)
            {
                throw new KnownException("USER_NOT_FOUND");
            }

            await mediator.Send(
                new BindAppleUserExternalIdentityCommand(
                    UserId: account.UserId,
                    SubjectId: principal.SubjectId),
                cancellationToken);
        }

        if (account.UserId is null)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        // 记录最后登录时间；若处于注销宽限期内，同时自动取消注销。
        await mediator.Send(new RecordUserLoginCommand(account.UserId), cancellationToken);

        var sessionTokens = await sessionTokenIssuer.GenerateAsync(account.UserId, account.EmailAddress, cancellationToken);
        // 自动注册的新账户没有机会在注册时提交邀请码：开通限时补交窗口，客户端据此弹窗引导。
        InvitationWindowInfo? invitationWindow = null;
        if (action == ExternalIdentityAuthenticationAction.Registered)
        {
            var windowToken = await invitationWindowStore.OpenAsync(account.UserId, cancellationToken);
            invitationWindow = new InvitationWindowInfo(windowToken, (int)InvitationWindowStore.WindowTtl.TotalSeconds);
        }

        var response = ExternalIdentityAuthenticationResponse.Build(action, sessionTokens, account, invitationWindow);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
