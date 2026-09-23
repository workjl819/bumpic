using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.Queries.Registration;
using Bumpic.Web.Services.EmailCodes;
using Bumpic.Web.Services.SessionTokens;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 邮箱登录请求。
/// </summary>
public record LoginWithEmailRequest
{
    /// <summary>
    /// 登录邮箱（注册时使用的邮箱）。
    /// </summary>
    /// <example>user@example.com</example>
    [Required]
    public string EmailAddress { get; init; } = string.Empty;

    /// <summary>
    /// 登录验证码：由 <c>POST /api/v1/email/send-code</c>（Purpose=Login）下发，6 位数字、10 分钟内有效、只能成功使用一次。
    /// </summary>
    /// <example>123456</example>
    [Required]
    public string EmailCode { get; init; } = string.Empty;
}

/// <summary>
/// 邮箱登录请求验证器：字段缺失或传 null 时统一返回参数校验错误，不进入业务逻辑。
/// </summary>
public class LoginWithEmailRequestValidator : Validator<LoginWithEmailRequest>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public LoginWithEmailRequestValidator()
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
/// 邮箱登录（匿名）：校验当次登录验证码，成功后返回会话令牌。验证码错误会计入失败次数，连续 5 次失败锁定 15 分钟；当前登录不使用密码。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/login")]
[AllowAnonymous]
public class LoginWithEmailEndpoint(
    IMediator mediator,
    IEmailCodeService emailCodeService,
    SessionTokenIssuer sessionTokenIssuer)
    : Endpoint<LoginWithEmailRequest, ResponseData<AuthenticationSessionResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(LoginWithEmailRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);
        try
        {
            await emailCodeService.VerifyAndConsumeAsync(email, OperationType.Login, request.EmailCode, cancellationToken);
        }
        catch (KnownException)
        {
            // 验证码错误与密码错误同等计入登录失败次数（失败计数属于账户聚合状态，通过 Command 记录）。
            await mediator.Send(new RecordLoginFailureCommand(EmailAddress: email), cancellationToken);
            throw;
        }

        var result = await mediator.Send(new LoginWithEmailCommand(EmailAddress: email), cancellationToken);

        var account = await mediator.Send(new GetUserAccountByEmailQuery(result.NormalizedEmail), cancellationToken);
        if (account.UserId is null)
        {
            throw new KnownException("INVALID_CREDENTIALS");
        }

        var sessionTokens = await sessionTokenIssuer.GenerateAsync(account.UserId, account.EmailAddress, cancellationToken);
        var response = AuthenticationSessionResponseFactory.Build(sessionTokens, account);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
