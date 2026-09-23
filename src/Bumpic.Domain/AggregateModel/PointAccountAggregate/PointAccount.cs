using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.PointAccountAggregate;

/// <summary>
/// 点数账户标识。
/// </summary>
public partial record PointAccountId : IGuidStronglyTypedId;

/// <summary>
/// 点数账户聚合根。
/// </summary>
public class PointAccount : Entity<PointAccountId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected PointAccount()
    {
    }

    /// <summary>
    /// 为用户创建点数账户（可用与冻结点数均为 0）。
    /// </summary>
    /// <param name="userAccountId">所属用户账户标识。</param>
    /// <returns>新建的点数账户。</returns>
    public static PointAccount Create(UserAccountId userAccountId)
    {
        if (userAccountId.Id == Guid.Empty)
        {
            throw new KnownException("USER_REQUIRED");
        }

        var now = DateTimeOffset.UtcNow;
        return new PointAccount
        {
            UserAccountId = userAccountId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 发放点数：增加可用点数并发布「点数已发放」领域事件；流水由事件处理器在独立聚合上追加。
    /// </summary>
    /// <param name="points">发放点数，必须为正。</param>
    /// <param name="recordType">对应流水类型。</param>
    /// <param name="businessReference">唯一业务引用，用于幂等。</param>
    public void Grant(int points, AccountPointRecordType recordType, string businessReference)
    {
        if (points <= 0)
        {
            throw new KnownException("INVALID_POINT_AMOUNT");
        }

        if (string.IsNullOrWhiteSpace(businessReference))
        {
            throw new KnownException("BUSINESS_REFERENCE_REQUIRED");
        }

        AvailablePoints += points;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new PointsGrantedDomainEvent(this, recordType, points, businessReference));
    }

    /// <summary>
    /// 所属用户账户标识。
    /// </summary>
    public UserAccountId UserAccountId { get; private set; } = new UserAccountId(Guid.Empty);

    /// <summary>
    /// 当前可用点数。
    /// </summary>
    public int AvailablePoints { get; private set; } = 0;

    /// <summary>
    /// 当前冻结点数。
    /// </summary>
    public int FrozenPoints { get; private set; } = 0;

    /// <summary>
    /// 点数账户创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// 点数账户最近更新时间。
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
    /// 逻辑删除PointAccount；账户注销流程调用，实体保留用于审计。
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


    /// <summary>
    /// 发放购买点数并追加不可变流水。
    /// </summary>
    public bool GrantPurchasedPoints(int points, string businessReference, DateTimeOffset now)
    {
        ValidatePositivePoints(points: points);
        ValidateBusinessReference(businessReference: businessReference);
        AvailablePoints = checked(AvailablePoints + points);
        UpdatedAt = now;
        AddDomainEvent(new PointBalanceChangedDomainEvent(
            PointAccount: this,
            RecordType: AccountPointRecordType.PurchaseGranted,
            Amount: points,
            BusinessReference: businessReference));
        return true;
    }

    /// <summary>
    /// 按权威退款事实扣回购买点数，允许形成负余额。
    /// </summary>
    public bool ReversePurchasedPoints(int points, string businessReference, DateTimeOffset now)
    {
        ValidatePositivePoints(points: points);
        ValidateBusinessReference(businessReference: businessReference);
        AvailablePoints = checked(AvailablePoints - points);
        UpdatedAt = now;
        AddDomainEvent(new PointBalanceChangedDomainEvent(
            PointAccount: this,
            RecordType: AccountPointRecordType.PurchaseReversed,
            Amount: -points,
            BusinessReference: businessReference));
        return true;
    }

    /// <summary>
    /// 在退款撤回后恢复此前冲正的购买点数。
    /// </summary>
    public bool ReinstatePurchasedPoints(int points, string businessReference, DateTimeOffset now)
    {
        ValidatePositivePoints(points: points);
        ValidateBusinessReference(businessReference: businessReference);
        AvailablePoints = checked(AvailablePoints + points);
        UpdatedAt = now;
        AddDomainEvent(new PointBalanceChangedDomainEvent(
            PointAccount: this,
            RecordType: AccountPointRecordType.PurchaseReinstated,
            Amount: points,
            BusinessReference: businessReference));
        return true;
    }

    private static void ValidateBusinessReference(string businessReference)
    {
        if (string.IsNullOrWhiteSpace(businessReference))
        {
            throw new ArgumentException("点数流水业务引用不能为空。", nameof(businessReference));
        }
    }

    private static void ValidatePositivePoints(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "点数变化值必须大于零。");
        }
    }
    
    /// <summary>
    /// 应用一次修复点数变化，固定五点；调用方必须按业务引用去重。
    /// </summary>
    /// <param name="userAccountId">任务所属用户。</param>
    /// <param name="recordType">冻结、结算或解冻。</param>
    /// <param name="businessReference">任务业务引用。</param>
    public void ApplyRestoration(UserAccountId userAccountId, AccountPointRecordType recordType, string businessReference)
    {
        if (Deleted || userAccountId != UserAccountId || string.IsNullOrWhiteSpace(businessReference))
        {
            throw new KnownException("POINT_ACCOUNT_USER_MISMATCH");
        }
        switch (recordType)
        {
            case AccountPointRecordType.RestorationFrozen:
                if (AvailablePoints < 5 || FrozenPoints != 0)
                {
                    throw new KnownException("INSUFFICIENT_POINTS");
                }
                AvailablePoints -= 5;
                FrozenPoints += 5;
                break;
            case AccountPointRecordType.RestorationSettled:
            case AccountPointRecordType.RestorationReleased:
                if (FrozenPoints != 5)
                {
                    throw new KnownException("INVALID_FROZEN_POINTS_STATE");
                }
                FrozenPoints -= 5;
                if (recordType == AccountPointRecordType.RestorationReleased)
                {
                    AvailablePoints += 5;
                }
                break;
            default:
                throw new KnownException("UNSUPPORTED_RESTORATION_POINTS_OPERATION");
        }
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new RestorationPointsChangedDomainEvent(PointAccount: this,
            RecordType: recordType, BusinessReference: businessReference));
    }
}
