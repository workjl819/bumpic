using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 外部身份绑定单元测试。
/// </summary>
public class UserExternalIdentityTests
{
    /// <summary>
    /// 绑定成功写入归属、提供方与主题标识。
    /// </summary>
    [Fact]
    public void Create_WithUserProviderSubject_CreatesIdentity()
    {
        var userId = new UserAccountId(Guid.NewGuid());

        var identity = UserExternalIdentity.Create(userId, UserExternalIdentityProvider.Apple, "apple-subject-123");

        Assert.Equal(userId, identity.UserAccountId);
        Assert.Equal(UserExternalIdentityProvider.Apple, identity.Provider);
        Assert.Equal("apple-subject-123", identity.SubjectId);
    }

    /// <summary>
    /// 空主题标识被拒绝。
    /// </summary>
    [Fact]
    public void Create_WithEmptySubject_ThrowsKnownException()
    {
        var userId = new UserAccountId(Guid.NewGuid());

        Assert.Throws<KnownException>(() =>
            UserExternalIdentity.Create(userId, UserExternalIdentityProvider.Google, "  "));
    }
}
