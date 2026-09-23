using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.Queries.Authentication;
using Bumpic.Web.Services.ExternalIdentities;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 账户注销请求。
/// </summary>
public record DeleteUserAccountRequest
{
    /// <summary>
    /// 二次确认标志，必须为 true。客户端需先向用户展示：宽限期内登录可取消注销；到期（见响应中的 scheduledDeletionAt）
    /// 后账户与外部身份将被注销，不可恢复。
    /// </summary>
    [Required]
    public bool Confirmed { get; init; }

    /// <summary>
    /// Apple 授权码；已绑定 Apple 身份时必填，用于一并解除 Apple 授权。未绑定 Apple 时可省略。
    /// </summary>
    public string? AppleAuthorizationCode { get; init; }

    /// <summary>
    /// Google access token；已绑定 Google 身份时必填，用于一并解除 Google 授权。未绑定 Google 时可省略。
    /// </summary>
    public string? GoogleAccessToken { get; init; }
}

/// <summary>
/// 账户注销响应。
/// </summary>
/// <param name="ScheduledDeletionAt">计划清除数据的时间；在此之前重新登录即自动取消注销。</param>
public record DeleteUserAccountResponse(DateTimeOffset ScheduledDeletionAt);

/// <summary>
/// 账户注销（需登录）：提交后进入宽限期（到期时间见响应 scheduledDeletionAt），宽限期内账户可正常使用、重新登录即自动取消注销；到期后清除账户数据。
/// </summary>
[Tags("User")]
[HttpPost("/api/v1/user/delete")]
[Authorize(Policy = PolicyNames.Client)]
public class DeleteUserAccountEndpoint(
    IMediator mediator,
    ILoginUser loginUser,
    IExternalIdentityRevocationService revocationService,
    IRevocationTokenProtector revocationTokenProtector)
    : Endpoint<DeleteUserAccountRequest, ResponseData<DeleteUserAccountResponse>>
{
    /// <inheritdoc />
    public override async Task HandleAsync(DeleteUserAccountRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
        {
            throw new KnownException("ACCOUNT_DELETION_NOT_CONFIRMED");
        }

        var userAccountId = new UserAccountId(loginUser.Id);
        var providers = await mediator.Send(
            new GetUserExternalIdentityProvidersQuery(userAccountId),
            cancellationToken);

        if (providers.Contains(UserExternalIdentityProvider.Apple)
            && string.IsNullOrWhiteSpace(request.AppleAuthorizationCode))
        {
            throw new KnownException("APPLE_AUTHORIZATION_CODE_REQUIRED");
        }

        if (providers.Contains(UserExternalIdentityProvider.Google)
            && string.IsNullOrWhiteSpace(request.GoogleAccessToken))
        {
            throw new KnownException("GOOGLE_ACCESS_TOKEN_REQUIRED");
        }

        // Google：本次请求内立即撤销授权（access token 不落库）。
        if (!string.IsNullOrWhiteSpace(request.GoogleAccessToken))
        {
            if (!await revocationService.RevokeGoogleAsync(request.GoogleAccessToken, cancellationToken))
            {
                throw new KnownException("GOOGLE_REVOCATION_FAILED");
            }
        }

        // Apple：用授权码换撤销令牌并加密，宽限期届满后由集成事件处理器解密撤销。
        string? appleRevocationTokenCiphertext = null;
        if (!string.IsNullOrWhiteSpace(request.AppleAuthorizationCode))
        {
            var refreshToken = await revocationService.ExchangeAppleRefreshTokenAsync(
                request.AppleAuthorizationCode,
                cancellationToken);
            if (refreshToken is null)
            {
                throw new KnownException("APPLE_REVOCATION_TOKEN_UNAVAILABLE");
            }

            appleRevocationTokenCiphertext = revocationTokenProtector.Protect(refreshToken);
        }

        var result = await mediator.Send(
            new RequestAccountDeletionCommand(
                UserAccountId: userAccountId,
                AppleRevocationTokenCiphertext: appleRevocationTokenCiphertext),
            cancellationToken);

        await Send.OkAsync(
            new DeleteUserAccountResponse(result.ScheduledDeletionAt).AsSuccessResponseData(),
            cancellation: cancellationToken);
    }
}
