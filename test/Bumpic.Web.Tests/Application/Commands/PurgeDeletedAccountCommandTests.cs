using Moq;
using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Application.Commands;

/// <summary>
/// 到期注销账户命令处理器单元测试（宽限期边界）。
/// </summary>
public class PurgeDeletedAccountCommandTests
{
    /// <summary>
    /// 测试使用的注销宽限期（模拟配置项 AccountDeletion:GracePeriod）。
    /// </summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    /// <summary>
    /// 宽限期未到时不执行软删除，也不发布注销事件。
    /// </summary>
    [Fact]
    public async Task Handle_WithinGracePeriod_DoesNotDelete()
    {
        var user = UserAccount.Register("within@example.com");
        user.RequestDeletion(DateTimeOffset.UtcNow.AddDays(-3), GracePeriod);
        var handler = CreateHandler(user);

        await handler.Handle(new PurgeDeletedAccountCommand(user.Id), CancellationToken.None);

        Assert.False(user.Deleted);
        Assert.Empty(user.GetDomainEvents().OfType<UserAccountDeletedDomainEvent>());
    }

    /// <summary>
    /// 用户在宽限期内取消注销后不执行软删除。
    /// </summary>
    [Fact]
    public async Task Handle_DeletionCancelled_DoesNotDelete()
    {
        var user = UserAccount.Register("cancelled@example.com");
        user.RequestDeletion(DateTimeOffset.UtcNow.AddDays(-40), GracePeriod);
        user.RecordLogin(DateTimeOffset.UtcNow.AddDays(-1));
        var handler = CreateHandler(user);

        await handler.Handle(new PurgeDeletedAccountCommand(user.Id), CancellationToken.None);

        Assert.False(user.Deleted);
    }

    /// <summary>
    /// 宽限期届满后软删除账户并发布注销事件。
    /// </summary>
    [Fact]
    public async Task Handle_GracePeriodExpired_SoftDeletesAccount()
    {
        var user = UserAccount.Register("expired@example.com");
        user.RequestDeletion(DateTimeOffset.UtcNow.Subtract(GracePeriod).AddMinutes(-1), GracePeriod);
        var handler = CreateHandler(user);

        await handler.Handle(new PurgeDeletedAccountCommand(user.Id), CancellationToken.None);

        Assert.True(user.Deleted);
        Assert.Equal(UserAccountStatus.Deleted, user.Status);
        Assert.Single(user.GetDomainEvents().OfType<UserAccountDeletedDomainEvent>());
    }

    private static PurgeDeletedAccountCommandHandler CreateHandler(UserAccount user)
    {
        var repository = new Mock<IUserAccountRepository>();
        repository
            .Setup(r => r.GetAsync(It.IsAny<UserAccountId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return new PurgeDeletedAccountCommandHandler(
            repository.Object,
            Microsoft.Extensions.Options.Options.Create(new AccountDeletionOptions { GracePeriod = GracePeriod }));
    }
}
