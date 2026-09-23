using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;

/// <summary>
/// 商店通知收件记录标识。
/// </summary>
public partial record StoreNotificationReceiptId : IGuidStronglyTypedId;

/// <summary>
/// 可重放的商店通知收件记录聚合根。
/// </summary>
public class StoreNotificationReceipt : Entity<StoreNotificationReceiptId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected StoreNotificationReceipt()
    {
    }

    /// <summary>
    /// 保存已验证来源且属于当前部署的商店通知。
    /// </summary>
    public StoreNotificationReceipt(
        AppStore store,
        string externalNotificationId,
        string notificationType,
        int schemaVersion,
        int parserVersion,
        string rawPayload,
        string? normalizedPayload,
        byte[] payloadHash,
        DateTimeOffset sourceVerifiedAt,
        string sourcePrincipal,
        string? sourceAudience,
        DateTimeOffset occurredAt,
        DateTimeOffset now)
    {
        ValidateIdentity(
            externalNotificationId: externalNotificationId,
            notificationType: notificationType,
            schemaVersion: schemaVersion,
            parserVersion: parserVersion);
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new ArgumentException("原始通知载荷不能为空。", nameof(rawPayload));
        }

        if (normalizedPayload is not null && string.IsNullOrWhiteSpace(normalizedPayload))
        {
            throw new ArgumentException("规范化通知载荷不能是空白字符串。", nameof(normalizedPayload));
        }

        ValidatePayloadHash(payloadHash: payloadHash);

        Store = store;
        ExternalNotificationId = externalNotificationId.Trim();
        NotificationType = notificationType.Trim();
        SchemaVersion = schemaVersion;
        ParserVersion = parserVersion;
        RawPayload = rawPayload;
        NormalizedPayload = normalizedPayload;
        PayloadHash = payloadHash.ToArray();
        SourceVerifiedAt = sourceVerifiedAt;
        SourcePrincipal = sourcePrincipal.Trim();
        SourceAudience = Normalize(value: sourceAudience);
        OccurredAt = occurredAt;
        Status = StoreNotificationReceiptStatus.Received;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public AppStore Store { get; private set; }
    public string ExternalNotificationId { get; private set; } = string.Empty;
    public string NotificationType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public int ParserVersion { get; private set; }
    public string RawPayload { get; private set; } = string.Empty;
    public string? NormalizedPayload { get; private set; }
    public byte[] PayloadHash { get; private set; } = [];
    public DateTimeOffset SourceVerifiedAt { get; private set; }
    public string SourcePrincipal { get; private set; } = string.Empty;
    public string? SourceAudience { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? LastReplayedAt { get; private set; }
    public int? LastReplayParserVersion { get; private set; }
    public StoreNotificationReceiptStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool Deleted { get; private set; }
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 尝试领取通知处理权，过期租约允许被接管。
    /// </summary>
    public StoreNotificationReceiptAcquireResult TryAcquire(
        string leaseOwner,
        TimeSpan leaseDuration,
        DateTimeOffset now)
    {
        if (Status == StoreNotificationReceiptStatus.Processed)
        {
            return StoreNotificationReceiptAcquireResult.Processed;
        }

        if (Status == StoreNotificationReceiptStatus.DeadLettered)
        {
            return StoreNotificationReceiptAcquireResult.DeadLettered;
        }

        if (Status == StoreNotificationReceiptStatus.Processing
            && LeaseExpiresAt.HasValue
            && LeaseExpiresAt.Value > now)
        {
            return StoreNotificationReceiptAcquireResult.Processing;
        }

        if (Status == StoreNotificationReceiptStatus.RetryScheduled
            && NextRetryAt.HasValue
            && NextRetryAt.Value > now)
        {
            return StoreNotificationReceiptAcquireResult.RetryNotDue;
        }

        if (string.IsNullOrWhiteSpace(leaseOwner) || leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException("通知处理租约所有者和时长必须有效。");
        }

        Status = StoreNotificationReceiptStatus.Processing;
        LeaseOwner = leaseOwner.Trim();
        LeaseExpiresAt = now.Add(leaseDuration);
        NextRetryAt = null;
        AttemptCount++;
        UpdatedAt = now;
        return StoreNotificationReceiptAcquireResult.Acquired;
    }

    /// <summary>
    /// 将当前租约持有者处理的通知标记为完成。
    /// </summary>
    public void Complete(string leaseOwner, DateTimeOffset now)
    {
        EnsureLeaseOwner(leaseOwner: leaseOwner, now: now);
        Status = StoreNotificationReceiptStatus.Processed;
        ProcessedAt = now;
        FailureCode = null;
        NextRetryAt = null;
        ClearLease();
        UpdatedAt = now;
    }

    /// <summary>
    /// 将当前处理失败安排为本地重试或死信。
    /// </summary>
    public void Fail(
        string leaseOwner,
        string failureCode,
        DateTimeOffset? nextRetryAt,
        bool deadLetter,
        DateTimeOffset now)
    {
        EnsureLeaseOwner(leaseOwner: leaseOwner, now: now);
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("通知失败码不能为空。", nameof(failureCode));
        }

        if (!deadLetter && (!nextRetryAt.HasValue || nextRetryAt.Value <= now))
        {
            throw new ArgumentException("可重试通知必须设置未来的重试时间。", nameof(nextRetryAt));
        }

        Status = deadLetter
            ? StoreNotificationReceiptStatus.DeadLettered
            : StoreNotificationReceiptStatus.RetryScheduled;
        FailureCode = failureCode.Trim();
        NextRetryAt = deadLetter ? null : nextRetryAt;
        ClearLease();
        UpdatedAt = now;
    }

    /// <summary>
    /// 记录使用新解析器完成的重放审计。
    /// </summary>
    public void RecordReplay(int parserVersion, DateTimeOffset now)
    {
        if (parserVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parserVersion));
        }

        LastReplayParserVersion = parserVersion;
        LastReplayedAt = now;
        UpdatedAt = now;
    }

    private void EnsureLeaseOwner(string leaseOwner, DateTimeOffset now)
    {
        if (Status != StoreNotificationReceiptStatus.Processing
            || !string.Equals(LeaseOwner, leaseOwner, StringComparison.Ordinal)
            || !LeaseExpiresAt.HasValue
            || LeaseExpiresAt.Value <= now)
        {
            throw new InvalidOperationException("当前执行者不持有有效通知处理租约。");
        }
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    private static void ValidateIdentity(
        string externalNotificationId,
        string notificationType,
        int schemaVersion,
        int parserVersion)
    {
        if (string.IsNullOrWhiteSpace(externalNotificationId)
            || string.IsNullOrWhiteSpace(notificationType)
            || schemaVersion <= 0
            || parserVersion <= 0)
        {
            throw new ArgumentException("通知身份、类型或版本不合法。");
        }
    }

    private static void ValidatePayloadHash(byte[] payloadHash)
    {
        if (payloadHash.Length != 32)
        {
            throw new ArgumentException("原始通知载荷摘要必须为 32 字节。", nameof(payloadHash));
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
