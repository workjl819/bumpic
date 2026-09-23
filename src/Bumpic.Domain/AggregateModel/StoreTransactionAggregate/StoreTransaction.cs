using Bumpic.Domain.AggregateModel.StoreProductAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.StoreTransactionAggregate;

/// <summary>
/// 商店交易标识。
/// </summary>
public partial record StoreTransactionId : IGuidStronglyTypedId;

/// <summary>
/// 商店交易聚合根。
/// </summary>
public class StoreTransaction : Entity<StoreTransactionId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected StoreTransaction()
    {
    }

    /// <summary>
    /// 创建已通过权威账户归属校验的 Apple 交易。
    /// </summary>
    public static StoreTransaction CreateLinkedApple(
        string externalTransactionId,
        UserAccountId userAccountId,
        DateTimeOffset now)
    {
        ValidateExternalTransactionId(externalTransactionId: externalTransactionId);

        return new StoreTransaction
        {
            Store = AppStore.AppleAppStore,
            ExternalTransactionId = externalTransactionId.Trim(),
            UserAccountId = userAccountId,
            OwnershipStatus = StoreTransactionOwnershipStatus.Linked,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 创建由可信通知发现、尚未归属用户的 Apple 交易。
    /// </summary>
    public static StoreTransaction CreateUnlinkedApple(
        string externalTransactionId,
        DateTimeOffset now)
    {
        ValidateExternalTransactionId(externalTransactionId: externalTransactionId);

        return new StoreTransaction
        {
            Store = AppStore.AppleAppStore,
            ExternalTransactionId = externalTransactionId.Trim(),
            OwnershipStatus = StoreTransactionOwnershipStatus.Unlinked,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 创建已通过权威账户归属校验的 Google 交易。
    /// </summary>
    public static StoreTransaction CreateLinkedGoogle(
        string externalTransactionId,
        UserAccountId userAccountId,
        bool isTestPurchase,
        DateTimeOffset now)
    {
        ValidateExternalTransactionId(externalTransactionId: externalTransactionId);

        return new StoreTransaction
        {
            Store = AppStore.GooglePlay,
            IsTestPurchase = isTestPurchase,
            ExternalTransactionId = externalTransactionId.Trim(),
            UserAccountId = userAccountId,
            OwnershipStatus = StoreTransactionOwnershipStatus.Linked,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 创建由可信 Google 通知发现、尚未归属用户的交易。
    /// </summary>
    public static StoreTransaction CreateUnlinkedGoogle(
        string externalTransactionId,
        bool isTestPurchase,
        DateTimeOffset now)
    {
        ValidateExternalTransactionId(externalTransactionId: externalTransactionId);

        return new StoreTransaction
        {
            Store = AppStore.GooglePlay,
            IsTestPurchase = isTestPurchase,
            ExternalTransactionId = externalTransactionId.Trim(),
            OwnershipStatus = StoreTransactionOwnershipStatus.Unlinked,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 交易来源商店。
    /// </summary>
    public AppStore Store { get; private set; } = AppStore.AppleAppStore;

    /// <summary>
    /// 是否为 Google 测试购买。
    /// </summary>
    public bool IsTestPurchase { get; private set; }

    /// <summary>
    /// 平台原始交易标识；Apple 保存 transactionId，Google 保存 purchaseToken。
    /// </summary>
    public string ExternalTransactionId { get; private set; } = string.Empty;

    /// <summary>
    /// 获得点数的用户账户标识。
    /// </summary>
    public UserAccountId? UserAccountId { get; private set; }

    /// <summary>
    /// 交易账户归属状态。
    /// </summary>
    public StoreTransactionOwnershipStatus OwnershipStatus { get; private set; }

    /// <summary>
    /// 逻辑关联的服务端商品标识。
    /// </summary>
    public StoreProductId? StoreProductId { get; private set; }

    /// <summary>
    /// 商店商品标识快照。
    /// </summary>
    public string? ProductId { get; private set; }

    /// <summary>
    /// 权威验单金额快照。
    /// </summary>
    public decimal? Amount { get; private set; }

    /// <summary>
    /// 权威验单币种快照。
    /// </summary>
    public string? CurrencyCode { get; private set; }

    /// <summary>
    /// 商品点数配置快照。
    /// </summary>
    public int PointsSnapshot { get; private set; }

    /// <summary>
    /// 该交易历史实际发放的点数。
    /// </summary>
    public int GrantedPoints { get; private set; }

    /// <summary>
    /// 该交易当前累计已冲正点数。
    /// </summary>
    public int ReversedPoints { get; private set; }

    /// <summary>
    /// 当前交易状态。
    /// </summary>
    public StoreTransactionStatus Status { get; private set; } = StoreTransactionStatus.PendingVerification;

    /// <summary>
    /// 验单证据摘要。
    /// </summary>
    public string? VerificationPayloadHash { get; private set; }

    /// <summary>
    /// 稳定失败原因。
    /// </summary>
    public string? FailureCode { get; private set; }

    /// <summary>
    /// Google 消费尝试次数。
    /// </summary>
    public int ConsumptionAttemptCount { get; private set; }

    /// <summary>
    /// 下一次补偿时间。
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; private set; }

    /// <summary>
    /// Apple 已合并的最新签名快照时间。
    /// </summary>
    public DateTimeOffset? LastPlatformVersionAt { get; private set; }

    /// <summary>
    /// Apple 最新签名快照在同一时间下的事实优先级。
    /// </summary>
    public AppleTransactionFactPriority? LastAppleFactPriority { get; private set; }

    /// <summary>
    /// 商店确认购买时间。
    /// </summary>
    public DateTimeOffset? PurchasedAt { get; private set; }

    /// <summary>
    /// 商店确认退款时间。
    /// </summary>
    public DateTimeOffset? RefundedAt { get; private set; }

    /// <summary>
    /// 交易记录创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// 交易记录最近更新时间。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// 软删除标记。
    /// </summary>
    public bool Deleted { get; private set; }

    /// <summary>
    /// 乐观并发控制版本。
    /// </summary>
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 在平台账户标识与目标用户匹配后认领未关联交易。
    /// </summary>
    public void LinkTo(UserAccountId userAccountId, DateTimeOffset now)
    {
        if (OwnershipStatus != StoreTransactionOwnershipStatus.Unlinked || UserAccountId is not null)
        {
            throw new InvalidOperationException("只有未关联交易可以被认领。");
        }

        UserAccountId = userAccountId;
        OwnershipStatus = StoreTransactionOwnershipStatus.Linked;
        UpdatedAt = now;
    }

    /// <summary>
    /// 保存权威商品与购买快照；快照早于已合并的平台事实时不写入。
    /// </summary>
    public bool CapturePurchase(
        StoreProductId storeProductId,
        string productId,
        int pointsSnapshot,
        DateTimeOffset purchasedAt,
        string verificationPayloadHash,
        decimal? amount,
        string? currencyCode,
        DateTimeOffset? platformVersionAt,
        DateTimeOffset now)
    {
        EnsureLinked();
        if (Status != StoreTransactionStatus.PendingVerification)
        {
            throw new InvalidOperationException("当前交易状态不能保存购买快照。");
        }

        if (pointsSnapshot <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsSnapshot), "点数快照必须大于零。");
        }

        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(verificationPayloadHash))
        {
            throw new ArgumentException("商品标识和验单证据摘要不能为空。");
        }

        if (StoreProductId is not null && StoreProductId != storeProductId)
        {
            throw new InvalidOperationException("已经冻结的商品不可更换。");
        }

        if (Store == AppStore.AppleAppStore
            && platformVersionAt.HasValue
            && !ShouldApplyApplePlatformFact(
                platformVersionAt: platformVersionAt.Value,
                priority: AppleTransactionFactPriority.Purchased))
        {
            return false;
        }

        StoreProductId = storeProductId;
        ProductId = productId.Trim();
        PointsSnapshot = pointsSnapshot;
        PurchasedAt = purchasedAt;
        VerificationPayloadHash = verificationPayloadHash.Trim();
        Amount = amount;
        CurrencyCode = Normalize(value: currencyCode)?.ToUpperInvariant();
        if (Store == AppStore.AppleAppStore && platformVersionAt.HasValue)
        {
            LastPlatformVersionAt = platformVersionAt;
            LastAppleFactPriority = AppleTransactionFactPriority.Purchased;
        }
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// 将 Google 交易标记为等待消费。
    /// </summary>
    public void MarkPendingConsumption(DateTimeOffset now)
    {
        EnsureLinked();
        if (Store != AppStore.GooglePlay || Status != StoreTransactionStatus.PendingVerification || PointsSnapshot <= 0)
        {
            throw new InvalidOperationException("当前交易不能进入等待消费状态。");
        }

        Status = StoreTransactionStatus.PendingConsumption;
        FailureCode = null;
        NextRetryAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 将尚未入账的 Google 延迟付款交易标记为已取消。
    /// </summary>
    public void MarkCanceled(DateTimeOffset now)
    {
        EnsureNotGranted(action: "取消");
        Status = StoreTransactionStatus.Canceled;
        FailureCode = "PURCHASE_CANCELED";
        NextRetryAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 根据 Google 权威快照取消尚未入账的延迟付款交易。
    /// </summary>
    public void ApplyGoogleCancellation(DateTimeOffset now)
    {
        if (Store != AppStore.GooglePlay)
        {
            throw new InvalidOperationException("该操作只适用于 Google Play 交易。");
        }

        if (Status is StoreTransactionStatus.Canceled
            or StoreTransactionStatus.Voided
            or StoreTransactionStatus.Refunded)
        {
            return;
        }

        if (Status != StoreTransactionStatus.PendingVerification)
        {
            throw new InvalidOperationException("只有尚未入账的 Google 延迟付款交易可以取消。");
        }

        MarkCanceled(now: now);
    }

    /// <summary>
    /// 将尚未入账的交易标记为入账前已退款或作废。
    /// </summary>
    public void MarkVoided(DateTimeOffset occurredAt, DateTimeOffset now)
    {
        EnsureNotGranted(action: "作废");
        Status = StoreTransactionStatus.Voided;
        FailureCode = "PURCHASE_REFUNDED";
        RefundedAt = occurredAt;
        NextRetryAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 记录 Google 消费临时失败并安排重试。
    /// </summary>
    public void RecordConsumptionFailure(string failureCode, DateTimeOffset nextRetryAt, DateTimeOffset now)
    {
        if (Status != StoreTransactionStatus.PendingConsumption)
        {
            throw new InvalidOperationException("只有等待消费的交易可以记录消费失败。");
        }

        ConsumptionAttemptCount++;
        FailureCode = failureCode;
        NextRetryAt = nextRetryAt;
        UpdatedAt = now;
    }

    /// <summary>
    /// 完成验单并发布点数发放事件。
    /// </summary>
    public void CompleteVerification(DateTimeOffset now)
    {
        EnsureLinked();
        var validState = Store == AppStore.AppleAppStore
            ? Status == StoreTransactionStatus.PendingVerification
            : Status == StoreTransactionStatus.PendingConsumption;
        if (!validState || PointsSnapshot <= 0 || StoreProductId is null || PurchasedAt is null)
        {
            throw new InvalidOperationException("交易尚未具备入账条件。");
        }

        Status = StoreTransactionStatus.Verified;
        GrantedPoints = PointsSnapshot;
        ReversedPoints = 0;
        FailureCode = null;
        NextRetryAt = null;
        UpdatedAt = now;

        this.AddDomainEvent(new StoreTransactionVerifiedDomainEvent(
            StoreTransactionId: Id,
            UserAccountId: UserAccountId!,
            GrantedPoints: GrantedPoints));
    }

    /// <summary>
    /// 根据 Apple 权威退款结果完整冲正整笔已发放点数。
    /// </summary>
    public int ApplyAppleRefund(
        DateTimeOffset refundedAt,
        DateTimeOffset platformVersionAt,
        string factKey,
        DateTimeOffset now)
    {
        ValidateFactKey(factKey: factKey);

        if (!ShouldApplyApplePlatformFact(
                platformVersionAt: platformVersionAt,
                priority: AppleTransactionFactPriority.RefundedOrVoided))
        {
            return 0;
        }

        LastPlatformVersionAt = platformVersionAt;
        LastAppleFactPriority = AppleTransactionFactPriority.RefundedOrVoided;
        RefundedAt = refundedAt;
        UpdatedAt = now;

        if (GrantedPoints == 0)
        {
            Status = StoreTransactionStatus.Voided;
            return 0;
        }

        return ApplyFullRefundCore(factKey: factKey, now: now);
    }

    /// <summary>
    /// 根据 Google 权威退款或作废结果完整冲正整笔已发放点数。
    /// </summary>
    public int ApplyGoogleFullRefund(
        DateTimeOffset refundedAt,
        string factKey,
        DateTimeOffset now)
    {
        if (Store != AppStore.GooglePlay)
        {
            throw new InvalidOperationException("该操作只适用于 Google Play 交易。");
        }

        ValidateFactKey(factKey: factKey);
        RefundedAt = refundedAt;
        UpdatedAt = now;
        if (GrantedPoints == 0)
        {
            Status = StoreTransactionStatus.Voided;
            return 0;
        }

        return ApplyFullRefundCore(factKey: factKey, now: now);
    }

    /// <summary>
    /// 应用 Apple 退款撤回并恢复当前累计冲正点数。
    /// </summary>
    public int ReinstateAppleRefund(
        DateTimeOffset platformVersionAt,
        string factKey,
        DateTimeOffset now)
    {
        ValidateFactKey(factKey: factKey);

        if (!ShouldApplyApplePlatformFact(
                platformVersionAt: platformVersionAt,
                priority: AppleTransactionFactPriority.RefundReversed))
        {
            return 0;
        }

        LastPlatformVersionAt = platformVersionAt;
        LastAppleFactPriority = AppleTransactionFactPriority.RefundReversed;
        UpdatedAt = now;

        if (GrantedPoints == 0)
        {
            if (Status == StoreTransactionStatus.Voided)
            {
                Status = StoreTransactionStatus.PendingVerification;
            }

            return 0;
        }

        var reinstatedPoints = ReversedPoints;
        Status = StoreTransactionStatus.Verified;
        ReversedPoints = 0;

        if (reinstatedPoints > 0)
        {
            this.AddDomainEvent(new StoreTransactionReinstatedDomainEvent(
                StoreTransactionId: Id,
                UserAccountId: RequireUserAccountId(),
                FactKey: factKey,
                ReinstatedPoints: reinstatedPoints));
        }

        return reinstatedPoints;
    }

    /// <summary>
    /// 判断是否已经合并了不早于指定平台版本的退款或作废事实。
    /// </summary>
    public bool HasMergedPlatformFactAtOrAfter(DateTimeOffset platformVersionAt)
    {
        return LastPlatformVersionAt.HasValue
               && LastPlatformVersionAt.Value >= platformVersionAt
               && LastAppleFactPriority.HasValue
               && LastAppleFactPriority.Value >= AppleTransactionFactPriority.RefundedOrVoided;
    }

    private void EnsureLinked()
    {
        if (OwnershipStatus != StoreTransactionOwnershipStatus.Linked || UserAccountId is null)
        {
            throw new InvalidOperationException("交易尚未通过权威账户归属校验。");
        }
    }

    private void EnsureNotGranted(string action)
    {
        if (GrantedPoints > 0)
        {
            throw new InvalidOperationException($"已经发放点数的交易不能执行{action}。");
        }
    }

    private bool ShouldApplyApplePlatformFact(
        DateTimeOffset platformVersionAt,
        AppleTransactionFactPriority priority)
    {
        if (Store != AppStore.AppleAppStore)
        {
            throw new InvalidOperationException("该操作只适用于 Apple 交易。");
        }

        if (!LastPlatformVersionAt.HasValue)
        {
            return true;
        }

        if (platformVersionAt > LastPlatformVersionAt.Value)
        {
            return true;
        }

        if (platformVersionAt < LastPlatformVersionAt.Value)
        {
            return false;
        }

        return !LastAppleFactPriority.HasValue || priority >= LastAppleFactPriority.Value;
    }

    private int ApplyFullRefundCore(string factKey, DateTimeOffset now)
    {
        var delta = GrantedPoints - ReversedPoints;
        ReversedPoints = GrantedPoints;
        Status = StoreTransactionStatus.Refunded;
        FailureCode = "PURCHASE_REFUNDED";
        NextRetryAt = null;
        if (delta > 0)
        {
            this.AddDomainEvent(new StoreTransactionReversedDomainEvent(
                StoreTransactionId: Id,
                UserAccountId: RequireUserAccountId(),
                FactKey: factKey,
                ReversedPoints: delta));
        }

        return delta;
    }

    private UserAccountId RequireUserAccountId()
    {
        EnsureLinked();
        return UserAccountId!;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateFactKey(string factKey)
    {
        if (string.IsNullOrWhiteSpace(factKey))
        {
            throw new ArgumentException("退款事实键不能为空。", nameof(factKey));
        }
    }

    private static void ValidateExternalTransactionId(string externalTransactionId)
    {
        if (string.IsNullOrWhiteSpace(externalTransactionId) || externalTransactionId.Trim().Length > 1000)
        {
            throw new ArgumentException("平台交易标识不能为空且不能超过 1000 个字符。", nameof(externalTransactionId));
        }

        if (!externalTransactionId.Trim().All(char.IsAscii))
        {
            throw new ArgumentException(
                "平台交易标识只允许 ASCII 字符，数据库按 ASCII 存储以支持唯一索引。",
                nameof(externalTransactionId));
        }
    }
}
