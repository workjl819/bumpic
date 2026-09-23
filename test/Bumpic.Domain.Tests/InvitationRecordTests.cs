using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 邀请关系领域行为单元测试。
/// </summary>
public class InvitationRecordTests
{
    /// <summary>
    /// 邀请奖励点数取产品策略常量（当前为 5 点）。
    /// </summary>
    [Fact]
    public void Establish_UsesPolicyRewardPoints()
    {
        var record = InvitationRecord.Establish(
            new UserAccountId(Guid.NewGuid()),
            new UserAccountId(Guid.NewGuid()),
            "ABCDEF123456");

        Assert.Equal(InvitationPolicy.RewardPoints, record.RewardPoints);
    }

    /// <summary>
    /// 建立邀请关系成功：双方标识、码快照、默认奖励点数（策略常量，默认 5 点）与领域事件正确。
    /// </summary>
    [Fact]
    public void Establish_WithValidInviterAndInvitee_CreatesRecordAndRaisesEvent()
    {
        var inviter = new UserAccountId(Guid.NewGuid());
        var invitee = new UserAccountId(Guid.NewGuid());
        const string code = "ABCD2345EFGH";

        var record = InvitationRecord.Establish(inviter, invitee, code);

        Assert.Equal(inviter, record.InviterUserAccountId);
        Assert.Equal(invitee, record.InviteeUserAccountId);
        Assert.Equal(code, record.InvitationCode);
        Assert.Equal(InvitationPolicy.RewardPoints, record.RewardPoints);
        Assert.True(record.EstablishedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        var domainEvent = Assert.Single(record.GetDomainEvents().OfType<InvitationEstablishedDomainEvent>());
        Assert.Same(record, domainEvent.InvitationRecord);
    }

    /// <summary>
    /// 邀请人与受邀用户相同时拒绝建立关系。
    /// </summary>
    [Fact]
    public void Establish_SelfInvitation_ThrowsKnownException()
    {
        var self = new UserAccountId(Guid.NewGuid());

        var exception = Assert.Throws<KnownException>(() =>
            InvitationRecord.Establish(self, self, "SELFCODE1234"));

        Assert.Equal("INVITATION_SELF", exception.Message);
    }

    /// <summary>
    /// 空邀请码拒绝建立关系。
    /// </summary>
    [Fact]
    public void Establish_EmptyCode_ThrowsKnownException()
    {
        var inviter = new UserAccountId(Guid.NewGuid());
        var invitee = new UserAccountId(Guid.NewGuid());

        Assert.Throws<KnownException>(() =>
            InvitationRecord.Establish(inviter, invitee, "  "));
    }

    /// <summary>
    /// 可自定义双方奖励点数。
    /// </summary>
    [Fact]
    public void Establish_WithCustomRewardPoints_KeepsGivenPoints()
    {
        var inviter = new UserAccountId(Guid.NewGuid());
        var invitee = new UserAccountId(Guid.NewGuid());

        var record = InvitationRecord.Establish(inviter, invitee, "CUSTOMPOINTS12", rewardPoints: 5);

        Assert.Equal(5, record.RewardPoints);
    }
}
