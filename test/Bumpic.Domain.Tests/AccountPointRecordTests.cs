using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 账户点数流水聚合行为单元测试。
/// </summary>
public class AccountPointRecordTests
{
    /// <summary>
    /// 追加流水写入归属、类型、金额与业务引用。
    /// </summary>
    [Fact]
    public void Create_WithValidInputs_CreatesRecord()
    {
        var pointAccountId = new PointAccountId(Guid.NewGuid());
        var userId = new UserAccountId(Guid.NewGuid());

        var record = AccountPointRecord.Create(
            pointAccountId: pointAccountId,
            userAccountId: userId,
            recordType: AccountPointRecordType.InvitationGranted,
            amount: 10,
            businessReference: "invitation:abc");

        Assert.Equal(pointAccountId, record.PointAccountId);
        Assert.Equal(userId, record.UserAccountId);
        Assert.Equal(AccountPointRecordType.InvitationGranted, record.Type);
        Assert.Equal(10, record.Amount);
        Assert.Equal("invitation:abc", record.BusinessReference);
    }

    /// <summary>
    /// 金额不能为零。
    /// </summary>
    [Fact]
    public void Create_WithZeroAmount_ThrowsKnownException()
    {
        Assert.Throws<KnownException>(() => AccountPointRecord.Create(
            pointAccountId: new PointAccountId(Guid.NewGuid()),
            userAccountId: new UserAccountId(Guid.NewGuid()),
            recordType: AccountPointRecordType.RegistrationGranted,
            amount: 0,
            businessReference: "registration:a@b.com"));
    }

    /// <summary>
    /// 退款冲正流水允许保存负数变化值。
    /// </summary>
    [Fact]
    public void Create_WithNegativeReversalAmount_CreatesRecord()
    {
        var record = AccountPointRecord.Create(
            pointAccountId: new PointAccountId(Guid.NewGuid()),
            userAccountId: new UserAccountId(Guid.NewGuid()),
            recordType: AccountPointRecordType.PurchaseReversed,
            amount: -100,
            businessReference: "REFUND:Apple:key:fact");

        Assert.Equal(-100, record.Amount);
        Assert.Equal(AccountPointRecordType.PurchaseReversed, record.Type);
    }

    /// <summary>
    /// 业务引用必填。
    /// </summary>
    [Fact]
    public void Create_WithEmptyBusinessReference_ThrowsKnownException()
    {
        Assert.Throws<KnownException>(() => AccountPointRecord.Create(
            pointAccountId: new PointAccountId(Guid.NewGuid()),
            userAccountId: new UserAccountId(Guid.NewGuid()),
            recordType: AccountPointRecordType.RegistrationGranted,
            amount: 10,
            businessReference: " "));
    }

    /// <summary>
    /// 归属标识必填。
    /// </summary>
    [Fact]
    public void Create_WithEmptyAccount_ThrowsKnownException()
    {
        Assert.Throws<KnownException>(() => AccountPointRecord.Create(
            pointAccountId: new PointAccountId(Guid.Empty),
            userAccountId: new UserAccountId(Guid.NewGuid()),
            recordType: AccountPointRecordType.RegistrationGranted,
            amount: 10,
            businessReference: "registration:a@b.com"));
    }
}
