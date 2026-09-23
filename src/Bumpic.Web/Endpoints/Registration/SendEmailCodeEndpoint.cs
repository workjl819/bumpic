using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Queries.Registration;
using Bumpic.Web.Services.EmailCodes;

namespace Bumpic.Web.Endpoints.Registration;

/// <summary>
/// 发送邮箱验证码请求。
/// </summary>
public record SendEmailCodeRequest
{
    /// <summary>
    /// 接收验证码的邮箱。
    /// </summary>
    /// <example>user@example.com</example>
    [Required]
    public string EmailAddress { get; init; } = string.Empty;

    /// <summary>
    /// 验证码用途：<c>Register</c>（邮箱未注册时注册）或 <c>Login</c>（邮箱已注册时登录）；兼容 1=Register、2=Login。
    /// 取值非法时返回「purpose 只能为 Register 或 Login」。
    /// </summary>
    /// <example>Register</example>
    [Required]
    [JsonConverter(typeof(Bumpic.Web.Utils.OperationTypeJsonConverter))]
    public OperationType Purpose { get; init; }

    /// <summary>
    /// 客户端设备标识（可选），用于限流辅助；同一设备请保持稳定。
    /// </summary>
    public string? DeviceId { get; init; }
}

/// <summary>
/// 发送邮箱验证码请求验证器：purpose 必须显式传 Register 或 Login（枚举缺省值为 Unknown，仅靠 [Required] 拦不住）。
/// </summary>
public class SendEmailCodeRequestValidator : Validator<SendEmailCodeRequest>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public SendEmailCodeRequestValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .MaximumLength(320).WithMessage("邮箱长度不能超过 320 个字符");
        RuleFor(x => x.Purpose)
            .Must(purpose => purpose is OperationType.Register or OperationType.Login)
            .WithMessage("purpose 只能为 Register 或 Login");
        RuleFor(x => x.DeviceId)
            .MaximumLength(128).WithMessage("设备标识长度不能超过 128 个字符");
    }
}

/// <summary>
/// 发送邮箱验证码响应。
/// </summary>
/// <param name="ResendAfterSeconds">重发冷却秒数；客户端可据此倒计时禁用重发按钮。</param>
public record SendEmailCodeResponse(int ResendAfterSeconds);

/// <summary>
/// 发送邮箱验证码（匿名）：下发 6 位数字验证码，10 分钟内有效。
/// 同一邮箱、IP 与设备的发送频率受限，冷却期内重复请求返回 EMAIL_RATE_LIMITED；Purpose=Register 且邮箱已注册时返回 EMAIL_ALREADY_REGISTERED。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/email/send-code")]
[AllowAnonymous]
public class SendEmailCodeEndpoint(IMediator mediator, IEmailCodeService emailCodeService)
    : Endpoint<SendEmailCodeRequest, ResponseData<SendEmailCodeResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(SendEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddressNormalizer.Normalize(request.EmailAddress);
        if (request.Purpose == OperationType.Register)
        {
            var registered = await mediator.Send(new CheckEmailRegisteredQuery(email), cancellationToken);
            if (registered.IsRegistered)
            {
                throw new KnownException("EMAIL_ALREADY_REGISTERED");
            }
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var resendAfterSeconds = await emailCodeService.SendCodeAsync(
            email,
            request.Purpose,
            ipAddress,
            request.DeviceId,
            cancellationToken);
        await Send.OkAsync(new SendEmailCodeResponse(resendAfterSeconds).AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
