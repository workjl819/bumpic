using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Web.Application.Commands.Authentication;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 注销请求。
/// </summary>
public record LogoutRequest
{
    /// <summary>
    /// 要注销的会话所对应的刷新令牌；仅撤销该会话，重复注销视为成功（幂等）。
    /// </summary>
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// 注销端点。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/logout")]
[AllowAnonymous]
public class LogoutEndpoint(IMediator mediator)
    : Endpoint<LogoutRequest, ResponseData<bool>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        await Send.OkAsync(true.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
