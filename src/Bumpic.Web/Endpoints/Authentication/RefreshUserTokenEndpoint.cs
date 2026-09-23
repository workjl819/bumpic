using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Web.Application.Commands.Authentication;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 刷新令牌请求。
/// </summary>
public record RefreshUserTokenRequest
{
    /// <summary>
    /// 登录/注册/刷新接口返回的刷新令牌（不透明字符串，不是 JWT）；每次刷新都会轮换，旧值立即失效。
    /// </summary>
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// 刷新令牌响应。
/// </summary>
/// <param name="AccessToken">新的访问令牌（JWT），请求时放入 Authorization: Bearer。</param>
/// <param name="RefreshToken">新的刷新令牌；请替换客户端保存的旧值。</param>
/// <param name="ExpiresInSeconds">新访问令牌的有效秒数。</param>
public record RefreshUserTokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// 轮换刷新令牌端点。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/token/refresh")]
[AllowAnonymous]
public class RefreshUserTokenEndpoint(IMediator mediator)
    : Endpoint<RefreshUserTokenRequest, ResponseData<RefreshUserTokenResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(RefreshUserTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RefreshUserTokenCommand(request.RefreshToken), cancellationToken);
        var response = new RefreshUserTokenResponse(
            AccessToken: result.AccessToken,
            RefreshToken: result.RefreshToken,
            ExpiresInSeconds: result.ExpiresInSeconds);
        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }
}
