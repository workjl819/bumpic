using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.IntegrationEvents;
using Bumpic.Web.Application.Queries.Authentication;
using Bumpic.Web.Services.ExternalIdentities;

namespace Bumpic.Web.Application.IntegrationEventHandlers;

/// <summary>
/// 账户注销后调用 Apple 撤销接口使授权失效：读取外部身份绑定表上的密文、解密、调用平台接口，成功后清除密文。
/// </summary>
/// <remarks>
/// 失败一律抛出异常交给 CAP 记录并重试，不在处理器内吞掉错误：
/// 撤销失败、应有令牌却缺失、密文无法解密都属于必须被观测到的异常，否则集成事件会显示成功而授权实际未撤销。
/// 仅当该用户根本没有对应平台的绑定记录（无需撤销）时正常返回。
/// </remarks>
public class UserAccountDeletedIntegrationEventHandlerForRevokeExternalIdentities(
    IMediator mediator,
    IExternalIdentityRevocationService revocationService,
    IRevocationTokenProtector revocationTokenProtector,
    ILogger<UserAccountDeletedIntegrationEventHandlerForRevokeExternalIdentities> logger)
    : IIntegrationEventHandler<UserAccountDeletedIntegrationEvent>
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <c>APPLE_REVOCATION_TOKEN_MISSING</c>：存在 Apple 绑定但未捕获撤销令牌；
    /// <c>APPLE_REVOCATION_TOKEN_UNDECRYPTABLE</c>：密文无法解密；
    /// <c>APPLE_REVOCATION_FAILED</c>：Apple 撤销接口调用失败。
    /// </exception>
    public async Task HandleAsync(UserAccountDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var token = await mediator.Send(
            new GetExternalIdentityRevocationTokenQuery(
                integrationEvent.UserAccountId,
                UserExternalIdentityProvider.Apple),
            cancellationToken);
        if (!token.BindingExists)
        {
            // 该账户从未绑定 Apple：无需撤销。
            return;
        }

        if (string.IsNullOrWhiteSpace(token.Ciphertext))
        {
            // 有 Apple 绑定却没有撤销令牌：注销请求阶段未捕获成功，属流程缺陷，必须抛出以便观测与补救。
            logger.LogWarning("Apple 撤销失败：账户 {UserAccountId} 存在 Apple 绑定但未捕获撤销令牌", integrationEvent.UserAccountId.Id);
            throw new InvalidOperationException("APPLE_REVOCATION_TOKEN_MISSING");
        }

        var refreshToken = revocationTokenProtector.Unprotect(token.Ciphertext);
        if (refreshToken is null)
        {
            // 密钥环丢失或密文损坏：保留密文以便修复后重试，并抛出异常交给 CAP 记录。
            logger.LogWarning("Apple 撤销失败：账户 {UserAccountId} 的撤销令牌无法解密（密钥环或密文异常）", integrationEvent.UserAccountId.Id);
            throw new InvalidOperationException("APPLE_REVOCATION_TOKEN_UNDECRYPTABLE");
        }

        var revoked = await revocationService.RevokeAppleAsync(refreshToken, cancellationToken);
        if (!revoked)
        {
            logger.LogWarning("Apple 撤销失败，交由 CAP 重试：账户 {UserAccountId}", integrationEvent.UserAccountId.Id);
            throw new InvalidOperationException("APPLE_REVOCATION_FAILED");
        }

        logger.LogInformation("Apple 授权已撤销：账户 {UserAccountId}", integrationEvent.UserAccountId.Id);
        await mediator.Send(
            new ClearExternalIdentityRevocationTokenCommand(
                integrationEvent.UserAccountId,
                UserExternalIdentityProvider.Apple),
            cancellationToken);
    }
}
