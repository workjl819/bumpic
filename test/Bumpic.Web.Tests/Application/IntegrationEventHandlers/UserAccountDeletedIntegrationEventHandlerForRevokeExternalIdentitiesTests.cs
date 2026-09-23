using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.IntegrationEventHandlers;
using Bumpic.Web.Application.IntegrationEvents;
using Bumpic.Web.Application.Queries.Authentication;
using Bumpic.Web.Services.ExternalIdentities;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Application.IntegrationEventHandlers;

/// <summary>
/// 账户注销后调用 Apple 撤销接口的集成事件处理器单元测试。
/// 失败必须抛异常交给 CAP 记录与重试，不得静默成功。
/// </summary>
public class UserAccountDeletedIntegrationEventHandlerForRevokeExternalIdentitiesTests
{
    private static readonly UserAccountId UserId = new(Guid.NewGuid());

    /// <summary>
    /// 该账户从未绑定 Apple 时无需撤销，正常返回。
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutAppleBinding_DoesNothing()
    {
        var (handler, mediator, revocation) = CreateHandler(bindingExists: false, ciphertext: string.Empty, refreshToken: null, revoked: true);

        await handler.HandleAsync(new UserAccountDeletedIntegrationEvent(UserId), CancellationToken.None);

        revocation.Verify(
            service => service.RevokeAppleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(mediator.Commands);
    }

    /// <summary>
    /// 有 Apple 绑定但没有撤销令牌：抛出异常以便在 CAP 中观测，且不清除任何数据。
    /// </summary>
    [Fact]
    public async Task HandleAsync_BindingWithoutToken_Throws()
    {
        var (handler, mediator, revocation) = CreateHandler(bindingExists: true, ciphertext: string.Empty, refreshToken: null, revoked: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UserAccountDeletedIntegrationEvent(UserId), CancellationToken.None));

        Assert.Equal("APPLE_REVOCATION_TOKEN_MISSING", exception.Message);
        revocation.Verify(
            service => service.RevokeAppleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(mediator.Commands);
    }

    /// <summary>
    /// 撤销成功后清除密文。
    /// </summary>
    [Fact]
    public async Task HandleAsync_RevokeSucceeds_ClearsToken()
    {
        var (handler, mediator, revocation) = CreateHandler(bindingExists: true, ciphertext: "cipher", refreshToken: "refresh", revoked: true);

        await handler.HandleAsync(new UserAccountDeletedIntegrationEvent(UserId), CancellationToken.None);

        revocation.Verify(
            service => service.RevokeAppleAsync("refresh", It.IsAny<CancellationToken>()),
            Times.Once);
        var command = Assert.IsType<ClearExternalIdentityRevocationTokenCommand>(Assert.Single(mediator.Commands));
        Assert.Equal(UserId, command.UserAccountId);
    }

    /// <summary>
    /// 撤销失败时抛错交给 CAP 重试，且不清除密文。
    /// </summary>
    [Fact]
    public async Task HandleAsync_RevokeFails_ThrowsAndKeepsToken()
    {
        var (handler, mediator, _) = CreateHandler(bindingExists: true, ciphertext: "cipher", refreshToken: "refresh", revoked: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UserAccountDeletedIntegrationEvent(UserId), CancellationToken.None));

        Assert.Equal("APPLE_REVOCATION_FAILED", exception.Message);
        Assert.Empty(mediator.Commands);
    }

    /// <summary>
    /// 密文无法解密时同样抛错（保留密文以便修复后重试），不静默成功。
    /// </summary>
    [Fact]
    public async Task HandleAsync_UndecryptableCiphertext_ThrowsWithoutRevoke()
    {
        var (handler, mediator, revocation) = CreateHandler(bindingExists: true, ciphertext: "cipher", refreshToken: null, revoked: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UserAccountDeletedIntegrationEvent(UserId), CancellationToken.None));

        Assert.Equal("APPLE_REVOCATION_TOKEN_UNDECRYPTABLE", exception.Message);
        revocation.Verify(
            service => service.RevokeAppleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(mediator.Commands);
    }

    private static (
        UserAccountDeletedIntegrationEventHandlerForRevokeExternalIdentities Handler,
        RecordingMediator Mediator,
        Mock<IExternalIdentityRevocationService> RevocationService) CreateHandler(
        bool bindingExists,
        string ciphertext,
        string? refreshToken,
        bool revoked)
    {
        var mediator = new RecordingMediator(request =>
            request is GetExternalIdentityRevocationTokenQuery
                ? new GetExternalIdentityRevocationTokenResponse(bindingExists, ciphertext)
                : null);

        var revocationService = new Mock<IExternalIdentityRevocationService>();
        revocationService
            .Setup(service => service.RevokeAppleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(revoked);

        var protector = new Mock<IRevocationTokenProtector>();
        protector
            .Setup(p => p.Unprotect(It.IsAny<string?>()))
            .Returns(refreshToken);

        var handler = new UserAccountDeletedIntegrationEventHandlerForRevokeExternalIdentities(
            mediator,
            revocationService.Object,
            protector.Object,
            NullLogger<UserAccountDeletedIntegrationEventHandlerForRevokeExternalIdentities>.Instance);

        return (handler, mediator, revocationService);
    }
}
