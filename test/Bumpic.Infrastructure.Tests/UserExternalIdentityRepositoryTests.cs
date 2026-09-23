using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure;
using Bumpic.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Bumpic.Infrastructure.Tests;

/// <summary>
/// 用户外部身份仓储测试。
/// </summary>
public class UserExternalIdentityRepositoryTests
{
    /// <summary>
    /// 账户软删除后仍可找到待清除的 Apple 撤销令牌。
    /// </summary>
    [Fact]
    public async Task FindByUserAndProviderIncludingDeletedAsync_DeletedBindingWithToken_ReturnsBinding()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options, new Mock<IMediator>().Object);
        var userId = new UserAccountId(Guid.NewGuid());
        var identity = UserExternalIdentity.Create(userId, UserExternalIdentityProvider.Apple, "apple-subject");
        identity.SetRevocationToken("encrypted-token", DateTimeOffset.UtcNow);
        identity.Delete(DateTimeOffset.UtcNow);
        context.UserExternalIdentities.Add(identity);
        await context.SaveChangesAsync();

        var repository = new UserExternalIdentityRepository(context);
        var found = await repository.FindByUserAndProviderIncludingDeletedAsync(
            userId,
            UserExternalIdentityProvider.Apple,
            CancellationToken.None);

        Assert.Same(identity, found);
    }
}
