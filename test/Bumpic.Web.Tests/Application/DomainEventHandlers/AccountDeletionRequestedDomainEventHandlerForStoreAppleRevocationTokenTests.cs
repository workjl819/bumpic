using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.DomainEventHandlers;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Application.DomainEventHandlers;

/// <summary>
/// 注销申请后保存 Apple 撤销令牌密文的领域事件处理器单元测试。
/// </summary>
public class AccountDeletionRequestedDomainEventHandlerForStoreAppleRevocationTokenTests
{
    /// <summary>
    /// 事件携带密文时写入外部身份绑定表。
    /// </summary>
    [Fact]
    public async Task Handle_WithCiphertext_StoresToken()
    {
        var user = UserAccount.Register("store-token@example.com");
        var mediator = new RecordingMediator();
        var handler = new AccountDeletionRequestedDomainEventHandlerForStoreAppleRevocationToken(mediator);

        await handler.Handle(
            new AccountDeletionRequestedDomainEvent(user, DateTimeOffset.UtcNow.AddDays(30), "cipher"),
            CancellationToken.None);

        var command = Assert.IsType<StoreExternalIdentityRevocationTokenCommand>(Assert.Single(mediator.Commands));
        Assert.Equal(user.Id, command.UserAccountId);
        Assert.Equal(UserExternalIdentityProvider.Apple, command.Provider);
        Assert.Equal("cipher", command.RevocationTokenCiphertext);
    }

    /// <summary>
    /// 事件未携带密文时不产生命令。
    /// </summary>
    [Fact]
    public async Task Handle_WithoutCiphertext_DoesNothing()
    {
        var user = UserAccount.Register("skip-token@example.com");
        var mediator = new RecordingMediator();
        var handler = new AccountDeletionRequestedDomainEventHandlerForStoreAppleRevocationToken(mediator);

        await handler.Handle(
            new AccountDeletionRequestedDomainEvent(user, DateTimeOffset.UtcNow.AddDays(30)),
            CancellationToken.None);

        Assert.Empty(mediator.Requests);
    }
}
