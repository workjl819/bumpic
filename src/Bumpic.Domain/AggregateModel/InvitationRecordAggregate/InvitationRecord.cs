using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;

namespace Bumpic.Domain.AggregateModel.InvitationRecordAggregate;

/// <summary>
/// 邀请记录标识。
/// </summary>
public partial record InvitationRecordId : IGuidStronglyTypedId;

/// <summary>
/// 邀请记录聚合根。
/// </summary>
public class InvitationRecord : Entity<InvitationRecordId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected InvitationRecord()
    {
    }

    /// <summary>
    /// 建立一条唯一邀请关系；禁止自邀。
    /// </summary>
    /// <param name="inviterUserAccountId">邀请人用户账户标识。</param>
    /// <param name="inviteeUserAccountId">受邀用户账户标识。</param>
    /// <param name="invitationCode">受邀用户提交的邀请码快照。</param>
    /// <param name="rewardPoints">发放给邀请人的点数，默认取 <see cref="InvitationPolicy.RewardPoints"/>。</param>
    /// <returns>新建的邀请记录。</returns>
    public static InvitationRecord Establish(
        UserAccountId inviterUserAccountId,
        UserAccountId inviteeUserAccountId,
        string invitationCode,
        int rewardPoints = InvitationPolicy.RewardPoints)
    {
        if (inviterUserAccountId == inviteeUserAccountId)
        {
            throw new KnownException("INVITATION_SELF");
        }

        if (string.IsNullOrWhiteSpace(invitationCode))
        {
            throw new KnownException("INVITATION_CODE_REQUIRED");
        }

        var now = DateTimeOffset.UtcNow;
        var invitationRecord = new InvitationRecord
        {
            InviterUserAccountId = inviterUserAccountId,
            InviteeUserAccountId = inviteeUserAccountId,
            InvitationCode = invitationCode,
            RewardPoints = rewardPoints,
            EstablishedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        invitationRecord.AddDomainEvent(new InvitationEstablishedDomainEvent(invitationRecord));
        return invitationRecord;
    }

    /// <summary>
    /// 建立邀请关系时使用的邀请码快照。
    /// </summary>
    public string InvitationCode { get; private set; } = string.Empty;

    /// <summary>
    /// 邀请人用户账户标识。
    /// </summary>
    public UserAccountId InviterUserAccountId { get; private set; } = new UserAccountId(Guid.Empty);

    /// <summary>
    /// 受邀用户账户标识。
    /// </summary>
    public UserAccountId InviteeUserAccountId { get; private set; } = new UserAccountId(Guid.Empty);

    /// <summary>
    /// 发放给邀请人的点数。
    /// </summary>
    public int RewardPoints { get; private set; } = InvitationPolicy.RewardPoints;

    /// <summary>
    /// 邀请关系建立时间。
    /// </summary>
    public DateTimeOffset EstablishedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 邀请记录创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 邀请记录最近更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 软删除标记。
    /// </summary>
    public bool Deleted { get; private set; } = false;

    /// <summary>
    /// 乐观并发控制版本。
    /// </summary>
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 逻辑删除InvitationRecord；账户注销流程调用，实体保留用于审计。
    /// </summary>
    /// <param name="now">当前时间。</param>
    public void Delete(DateTimeOffset now)
    {
        if (Deleted)
        {
            return;
        }

        Deleted = true;
        UpdatedAt = now;
    }

}