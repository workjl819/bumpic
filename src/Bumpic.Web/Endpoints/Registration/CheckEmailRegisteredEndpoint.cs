using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Web.Application.Queries.Registration;

namespace Bumpic.Web.Endpoints.Registration;

/// <summary>
/// 邮箱是否已注册请求。
/// </summary>
public record CheckEmailRegisteredRequest
{
    /// <summary>
    /// 待检查的邮箱；客户端据此决定走注册还是登录流程。
    /// </summary>
    /// <example>user@example.com</example>
    [Required]
    public string EmailAddress { get; init; } = string.Empty;
}

/// <summary>
/// 邮箱是否已注册请求验证器。
/// </summary>
public class CheckEmailRegisteredRequestValidator : Validator<CheckEmailRegisteredRequest>
{
    /// <summary>
    /// 构造验证器。
    /// </summary>
    public CheckEmailRegisteredRequestValidator()
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .MaximumLength(320).WithMessage("邮箱长度不能超过 320 个字符");
    }
}

/// <summary>
/// 查询邮箱是否已注册。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/check-email")]
[AllowAnonymous]
public class CheckEmailRegisteredEndpoint(IMediator mediator)
    : Endpoint<CheckEmailRegisteredRequest, ResponseData<CheckEmailRegisteredResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(CheckEmailRegisteredRequest request, CancellationToken cancellationToken)
    {
        var response = await mediator.Send(new CheckEmailRegisteredQuery(request.EmailAddress), cancellationToken);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
