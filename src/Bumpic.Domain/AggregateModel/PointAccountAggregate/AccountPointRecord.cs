using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.PointAccountAggregate;

/// <summary>
/// 账户点数记录标识。
/// </summary>
public partial record AccountPointRecordId : IGuidStronglyTypedId;

/// <summary>
/// 账户点数流水聚合根：独立表保存，通过点数账户标识逻辑关联，不建立数据库外键。
/// </summary>
public class AccountPointRecord : Entity<AccountPointRecordId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected AccountPointRecord()
    {
    }

    /// <summary>
    /// 追加一条点数流水。
    /// </summary>
    /// <param name="pointAccountId">逻辑关联的点数账户标识。</param>
    /// <param name="userAccountId">冗余保存的用户账户标识，必须与点数账户归属一致。</param>
    /// <param name="recordType">流水类型。</param>
    /// <param name="amount">本次点数变化值，增加为正、扣减为负且不能为零。</param>
    /// <param name="businessReference">唯一业务引用，用于幂等。</param>
    /// <returns>新建的点数流水记录。</returns>
    public static AccountPointRecord Create(
        PointAccountId pointAccountId,
        UserAccountId userAccountId,
        AccountPointRecordType recordType,
        int amount,
        string businessReference)
    {
        if (pointAccountId.Id == Guid.Empty || userAccountId.Id == Guid.Empty)
        {
            throw new KnownException("ACCOUNT_REQUIRED");
        }

        if (amount == 0)
        {
            throw new KnownException("INVALID_POINT_AMOUNT");
        }

        if (string.IsNullOrWhiteSpace(businessReference))
        {
            throw new KnownException("BUSINESS_REFERENCE_REQUIRED");
        }

        var now = DateTimeOffset.UtcNow;
        return new AccountPointRecord
        {
            PointAccountId = pointAccountId,
            UserAccountId = userAccountId,
            Type = recordType,
            Amount = amount,
            BusinessReference = businessReference,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 逻辑关联的点数账户标识。
    /// </summary>
    public PointAccountId PointAccountId { get; private set; } = new PointAccountId(Guid.Empty);

    /// <summary>
    /// 冗余保存的用户账户标识。
    /// </summary>
    public UserAccountId UserAccountId { get; private set; } = new UserAccountId(Guid.Empty);

    /// <summary>
    /// 点数流水类型。
    /// </summary>
    public AccountPointRecordType Type { get; private set; } = AccountPointRecordType.RegistrationGranted;

    /// <summary>
    /// 本次点数变化值。
    /// </summary>
    public int Amount { get; private set; } = 0;

    /// <summary>
    /// 关联任务、交易或邀请的业务引用。
    /// </summary>
    public string BusinessReference { get; private set; } = string.Empty;

    /// <summary>
    /// 流水创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 流水更新时间。
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
    /// 逻辑删除AccountPointRecord；账户注销流程调用，实体保留用于审计。
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
