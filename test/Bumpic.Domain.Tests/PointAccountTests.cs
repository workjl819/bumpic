using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 点数账户聚合行为单元测试。
/// </summary>
public class PointAccountTests
{
    
    /// <summary>
    /// 创建点数账户时可用与冻结点数均为 0。
    /// </summary>
    [Fact]
    public void Create_WithUser_CreatesAccountWithZeroBalance()
    {
        var userId = new UserAccountId(Guid.NewGuid());

        var account = PointAccount.Create(userId);

        Assert.Equal(userId, account.UserAccountId);
        Assert.Equal(0, account.AvailablePoints);
        Assert.Equal(0, account.FrozenPoints);
    }

    /// <summary>
    /// 空用户标识创建被拒绝。
    /// </summary>
    [Fact]
    public void Create_WithEmptyUser_ThrowsKnownException()
    {
        Assert.Throws<KnownException>(() => PointAccount.Create(new UserAccountId(Guid.Empty)));
    }

    /// <summary>
    /// 发放点数增加可用点数并发布点数已发放事件（携带类型、数量与业务引用）。
    /// </summary>
    [Fact]
    public void Grant_WithPositivePoints_IncreasesBalanceAndRaisesEvent()
    {
        var account = PointAccount.Create(new UserAccountId(Guid.NewGuid()));

        account.Grant(points: 10, recordType: AccountPointRecordType.RegistrationGranted, businessReference: "registration:a@b.com");

        Assert.Equal(10, account.AvailablePoints);
        var domainEvent = Assert.Single(account.GetDomainEvents().OfType<PointsGrantedDomainEvent>());
        Assert.Equal(10, domainEvent.Amount);
        Assert.Equal(AccountPointRecordType.RegistrationGranted, domainEvent.RecordType);
        Assert.Equal("registration:a@b.com", domainEvent.BusinessReference);
        Assert.Same(account, domainEvent.PointAccount);
    }

    /// <summary>
    /// 发放点数必须为正。
    /// </summary>
    [Fact]
    public void Grant_WithNonPositivePoints_ThrowsKnownException()
    {
        var account = PointAccount.Create(new UserAccountId(Guid.NewGuid()));

        Assert.Throws<KnownException>(() => account.Grant(0, AccountPointRecordType.InvitationGranted, "invitation:1"));
    }

    /// <summary>
    /// 业务引用必填（幂等键）。
    /// </summary>
    [Fact]
    public void Grant_WithEmptyBusinessReference_ThrowsKnownException()
    {
        var account = PointAccount.Create(new UserAccountId(Guid.NewGuid()));

        Assert.Throws<KnownException>(() => account.Grant(10, AccountPointRecordType.InvitationGranted, "  "));
    }
    
    /// <summary>
    /// 退款冲正允许账户形成负余额并发布负数流水事件。
    /// </summary>
    [Fact]
    public void ReversePurchasedPoints_Should_Allow_Negative_Balance_And_Raise_Event()
    {
        var now = DateTimeOffset.UtcNow;
        var account = PointAccount.Create(new UserAccountId(Guid.NewGuid()));
        account.GrantPurchasedPoints(
            points: 15,
            businessReference: "PURCHASE:Apple:previous-purchase",
            now: now);

        var applied = account.ReversePurchasedPoints(
            points: 100,
            businessReference: "REFUND:Apple:key:fact-1",
            now: now);

        Assert.True(applied);
        Assert.Equal(-85, account.AvailablePoints);
        var domainEvent = account.GetDomainEvents()
            .OfType<PointBalanceChangedDomainEvent>()
            .Last();
        Assert.Equal(AccountPointRecordType.PurchaseReversed, domainEvent.RecordType);
        Assert.Equal(-100, domainEvent.Amount);
        Assert.Equal("REFUND:Apple:key:fact-1", domainEvent.BusinessReference);
    }

    /// <summary>
    /// 退款撤回恢复点数并产生独立流水。
    /// </summary>
    [Fact]
    public void ReinstatePurchasedPoints_Should_Restore_Reversed_Points()
    {
        var now = DateTimeOffset.UtcNow;
        var account = PointAccount.Create(new UserAccountId(Guid.NewGuid()));
        account.GrantPurchasedPoints(
            points: 100,
            businessReference: "PURCHASE:Apple:key",
            now: now);
        account.ReversePurchasedPoints(
            points: 85,
            businessReference: "REFUND:Apple:key:fact-1",
            now: now.AddMinutes(1));

        account.ReinstatePurchasedPoints(
            points: 85,
            businessReference: "REFUND_REVERSED:Apple:key:fact-2",
            now: now.AddMinutes(2));

        Assert.Equal(100, account.AvailablePoints);
        var domainEvent = account.GetDomainEvents()
            .OfType<PointBalanceChangedDomainEvent>()
            .Last();
        Assert.Equal(AccountPointRecordType.PurchaseReinstated, domainEvent.RecordType);
        Assert.Equal(85, domainEvent.Amount);
    }
}
