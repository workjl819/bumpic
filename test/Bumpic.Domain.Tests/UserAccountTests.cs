using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 用户账户注册领域行为单元测试。
/// </summary>
public class UserAccountTests
{
    /// <summary>
    /// 注册成功创建状态为 Active 的账户、生成邀请码并发布注册领域事件（不传密码）。
    /// </summary>
    [Fact]
    public void Register_WithEmail_CreatesActiveAccountWithCodeAndRaisesEvent()
    {
        const string email = "User@Example.com";

        var user = UserAccount.Register(email);

        Assert.Equal(email, user.EmailAddress);
        Assert.Equal(string.Empty, user.PasswordHash);
        Assert.Equal(UserAccountStatus.Active, user.Status);
        Assert.False(string.IsNullOrWhiteSpace(user.InvitationCode));
        Assert.Equal(InvitationCodeGenerator.DefaultLength, user.InvitationCode.Length);
        Assert.NotEqual(Guid.Empty, user.PurchaseAccountToken);
        Assert.Single(user.GetDomainEvents().OfType<UserAccountRegisteredDomainEvent>());
    }

    /// <summary>
    /// 两次注册生成的邀请码互不相同。
    /// </summary>
    [Fact]
    public void Register_Twice_ProducesDifferentInvitationCodes()
    {
        var first = UserAccount.Register("first@example.com");
        var second = UserAccount.Register("second@example.com");

        Assert.NotEqual(first.InvitationCode, second.InvitationCode);
    }

    /// <summary>
    /// 不同账户生成不同的购买账户标识。
    /// </summary>
    [Fact]
    public void Register_Twice_ProducesDifferentPurchaseAccountTokens()
    {
        var first = UserAccount.Register("first-purchase@example.com");
        var second = UserAccount.Register("second-purchase@example.com");

        Assert.NotEqual(first.PurchaseAccountToken, second.PurchaseAccountToken);
    }

    /// <summary>
    /// 注册事件载荷指向同一账户实例。
    /// </summary>
    [Fact]
    public void Register_EventPayload_ReferencesSameAccount()
    {
        var user = UserAccount.Register("event@example.com");

        var domainEvent = Assert.Single(user.GetDomainEvents().OfType<UserAccountRegisteredDomainEvent>());

        Assert.Same(user, domainEvent.UserAccount);
    }

    /// <summary>
    /// 空邮箱注册被拒绝。
    /// </summary>
    [Fact]
    public void Register_EmptyEmail_ThrowsKnownException()
    {
        Assert.Throws<KnownException>(() => UserAccount.Register("  "));
    }

    /// <summary>
    /// 保留的密码摘要参数仍可写入（供后续扩展）。
    /// </summary>
    [Fact]
    public void Register_WithPasswordHash_StoresProvidedHash()
    {
        var user = UserAccount.Register("with-hash@example.com", "stored-hash");

        Assert.Equal("stored-hash", user.PasswordHash);
    }

    /// <summary>
    /// 已有邀请码时补生成操作保持原码不变且不新增领域事件。
    /// </summary>
    [Fact]
    public void EnsureInvitationCode_WhenAlreadyPresent_KeepsCodeAndEvents()
    {
        var user = UserAccount.Register("ensure@example.com");
        var codeBefore = user.InvitationCode;

        user.EnsureInvitationCode();

        Assert.Equal(codeBefore, user.InvitationCode);
        Assert.Single(user.GetDomainEvents().OfType<UserAccountRegisteredDomainEvent>());
    }

    /// <summary>
    /// 注册即登录：注册成功时记录首次登录时间。
    /// </summary>
    [Fact]
    public void Register_RecordsFirstLoginTime()
    {
        var before = DateTimeOffset.UtcNow;

        var user = UserAccount.Register("first-login@example.com");

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= before);
    }

}
