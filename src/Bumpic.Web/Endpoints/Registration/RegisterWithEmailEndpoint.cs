using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Web.Application.Commands.Registration;
using Bumpic.Web.Application.Queries.Registration;
using Bumpic.Domain;
using Bumpic.Domain.Enums;
using Bumpic.Web.Endpoints.Authentication;
using Bumpic.Web.Services.EmailCodes;
using Bumpic.Web.Services.SessionTokens;

namespace Bumpic.Web.Endpoints.Registration;

/// <summary>
/// 邮箱注册请求。
/// </summary>
public record RegisterWithEmailRequest
{
    /// <summary>
    /// 登录邮箱（注册后不可自助修改，请确认无误）。
    /// </summary>
    /// <example>user@example.com</example>
    [Required]
    public string EmailAddress { get; init; } = string.Empty;

    /// <summary>
    /// 注册验证码：由 <c>POST /api/v1/email/send-code</c>（Purpose=Register）下发，6 位数字、10 分钟内有效、只能成功使用一次。
    /// </summary>
    /// <example>123456</example>
    [Required]
    public string EmailCode { get; init; } = string.Empty;


}

/// <summary>
/// 邮箱注册请求验证器：字段缺失或传 null 时统一返回参数校验错误，不进入业务逻辑。
/// </summary>
public class RegisterWithEmailRequestValidator : Validator<RegisterWithEmailRequest>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public RegisterWithEmailRequestValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .MaximumLength(320).WithMessage("邮箱长度不能超过 320 个字符");
        RuleFor(x => x.EmailCode)
            .NotEmpty().WithMessage("验证码不能为空")
            .MaximumLength(16).WithMessage("验证码长度不能超过 16 个字符");
    }
}

/// <summary>
/// 邮箱注册（匿名）：校验注册验证码后创建账户并直接登录，返回会话令牌；当前注册不使用密码。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/register")]
[AllowAnonymous]
public class RegisterWithEmailEndpoint(
    IMediator mediator,
    IEmailCodeService emailCodeService,
    SessionTokenIssuer sessionTokenIssuer)
    : Endpoint<RegisterWithEmailRequest, ResponseData<AuthenticationSessionResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(RegisterWithEmailRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);

        // 已注册邮箱直接返回统一错误，不消耗验证码。
        var alreadyRegistered = await mediator.Send(new CheckEmailRegisteredQuery(email), cancellationToken);
        if (alreadyRegistered.IsRegistered)
        {
            throw new KnownException("EMAIL_ALREADY_REGISTERED");
        }

        await emailCodeService.VerifyAndConsumeAsync(email, OperationType.Register, request.EmailCode, cancellationToken);

        var registered = await mediator.Send(
            new RegisterWithEmailCommand(
                EmailAddress: email),
            cancellationToken);

        var account = await mediator.Send(
            new GetUserAccountByEmailQuery(registered.NormalizedEmail),
            cancellationToken);
        if (!account.Found || account.UserId is null)
        {
            throw new KnownException("REGISTRATION_FAILED");
        }

        var sessionTokens = await sessionTokenIssuer.GenerateAsync(
            account.UserId,
            account.EmailAddress,
            cancellationToken);
        var response = AuthenticationSessionResponseFactory.Build(sessionTokens, account);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
