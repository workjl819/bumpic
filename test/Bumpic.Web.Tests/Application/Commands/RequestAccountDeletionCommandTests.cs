using Moq;
using NetCorePal.Extensions.Primitives;
using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Application.Commands;

/// <summary>
/// 提交账户注销命令处理器单元测试。
/// </summary>
public class RequestAccountDeletionCommandTests
{
    /// <summary>
    /// 测试使用的注销宽限期（模拟配置项 AccountDeletion:GracePeriod）。
    /// </summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 提交成功后返回宽限期到期时间，并把 Apple 撤销令牌密文交给领域方法。
    /// </summary>
    [Fact]
    public async Task Handle_ExistingAccount_MarksPendingAndReturnsSchedule()
    {
        var user = UserAccount.Register("request@example.com");
        var before = DateTimeOffset.UtcNow;
        var handler = CreateHandler(user);

        var result = await handler.Handle(
            new RequestAccountDeletionCommand(user.Id, "apple-cipher"),
            CancellationToken.None);

        Assert.True(user.IsDeletionPending);
        Assert.Equal(before.Add(GracePeriod), result.ScheduledDeletionAt, TimeSpan.FromSeconds(5));
        var domainEvent = Assert.Single(user.GetDomainEvents().OfType<AccountDeletionRequestedDomainEvent>());
        Assert.Equal("apple-cipher", domainEvent.AppleRevocationTokenCiphertext);
    }

    /// <summary>
    /// 账户不存在时返回 USER_NOT_FOUND。
    /// </summary>
    [Fact]
    public async Task Handle_AccountNotFound_Throws()
    {
        var handler = CreateHandler(user: null);

        var exception = await Assert.ThrowsAsync<KnownException>(() =>
            handler.Handle(new RequestAccountDeletionCommand(new UserAccountId(Guid.NewGuid())), CancellationToken.None));

        Assert.Equal("USER_NOT_FOUND", exception.Message);
    }

    /// <summary>
    /// 已注销账户不允许重复提交。
    /// </summary>
    [Fact]
    public async Task Handle_DeletedAccount_Throws()
    {
        var user = UserAccount.Register("deleted-request@example.com");
        user.Delete(DateTimeOffset.UtcNow);
        var handler = CreateHandler(user);

        var exception = await Assert.ThrowsAsync<KnownException>(() =>
            handler.Handle(new RequestAccountDeletionCommand(user.Id), CancellationToken.None));

        Assert.Equal("USER_NOT_FOUND", exception.Message);
    }

    private static RequestAccountDeletionCommandHandler CreateHandler(UserAccount? user)
    {
        var repository = new Mock<IUserAccountRepository>();
        repository
            .Setup(r => r.GetAsync(It.IsAny<UserAccountId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return new RequestAccountDeletionCommandHandler(
            repository.Object,
            Microsoft.Extensions.Options.Options.Create(new AccountDeletionOptions { GracePeriod = GracePeriod }));
    }
}
