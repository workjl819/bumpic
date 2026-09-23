using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate;

/// <summary>
/// 商店交易事实标识。
/// </summary>
public partial record StoreTransactionFactId : IGuidStronglyTypedId;

/// <summary>
/// 只追加的商店交易事实聚合根。
/// </summary>
public class StoreTransactionFact : Entity<StoreTransactionFactId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected StoreTransactionFact()
    {
    }

    /// <summary>
    /// 保存来自通知、客户端验单或对账的权威平台事实。
    /// </summary>
    public StoreTransactionFact(
        StoreNotificationReceiptId? storeNotificationReceiptId,
        StoreTransactionFactSourceType sourceType,
        AppStore store,
        string externalTransactionId,
        string? storeOrderId,
        string externalEventId,
        StoreTransactionFactType type,
        DateTimeOffset occurredAt,
        DateTimeOffset? platformVersionAt,
        DateTimeOffset observedAt,
        byte[] factKey,
        byte[] payloadHash,
        DateTimeOffset now)
    {
        ValidateHash(value: factKey, parameterName: nameof(factKey));
        ValidateHash(value: payloadHash, parameterName: nameof(payloadHash));
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

        if (string.IsNullOrWhiteSpace(externalEventId))
        {
            throw new ArgumentException("平台事实来源事件标识不能为空。", nameof(externalEventId));
        }

        if (storeOrderId?.Trim().Length > 255)
        {
            throw new ArgumentException("商店订单标识不能超过 255 个字符。", nameof(storeOrderId));
        }

        if (storeOrderId is not null && !storeOrderId.Trim().All(char.IsAscii))
        {
            throw new ArgumentException("商店订单标识只允许 ASCII 字符。", nameof(storeOrderId));
        }

        if (sourceType == StoreTransactionFactSourceType.Notification
            && storeNotificationReceiptId is null)
        {
            throw new ArgumentException("通知来源的交易事实必须关联收件记录。", nameof(storeNotificationReceiptId));
        }

        StoreNotificationReceiptId = storeNotificationReceiptId;
        SourceType = sourceType;
        Store = store;
        ExternalTransactionId = externalTransactionId.Trim();
        StoreOrderId = Normalize(value: storeOrderId);
        ExternalEventId = externalEventId.Trim();
        Type = type;
        OccurredAt = occurredAt;
        PlatformVersionAt = platformVersionAt;
        ObservedAt = observedAt;
        FactKey = factKey.ToArray();
        PayloadHash = payloadHash.ToArray();
        CreatedAt = now;
        UpdatedAt = now;
    }

    public StoreNotificationReceiptId? StoreNotificationReceiptId { get; private set; }
    public StoreTransactionId? StoreTransactionId { get; private set; }
    public StoreTransactionFactSourceType SourceType { get; private set; }
    public AppStore Store { get; private set; }
    public string ExternalTransactionId { get; private set; } = string.Empty;
    public string? StoreOrderId { get; private set; }
    public string ExternalEventId { get; private set; } = string.Empty;
    public StoreTransactionFactType Type { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PlatformVersionAt { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
    public byte[] FactKey { get; private set; } = [];
    public byte[] PayloadHash { get; private set; } = [];
    public DateTimeOffset? AppliedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool Deleted { get; private set; }
    public RowVersion RowVersion { get; private set; } = new(0);

    /// <summary>
    /// 标记事实已经由指定交易聚合完成合并。
    /// </summary>
    public void MarkApplied(StoreTransactionId storeTransactionId, DateTimeOffset now)
    {
        if (AppliedAt.HasValue)
        {
            if (StoreTransactionId == storeTransactionId)
            {
                return;
            }

            throw new InvalidOperationException("交易事实不能重复应用到不同交易。");
        }

        StoreTransactionId = storeTransactionId;
        AppliedAt = now;
        UpdatedAt = now;
    }

    private static void ValidateHash(byte[] value, string parameterName)
    {
        if (value.Length != 32)
        {
            throw new ArgumentException("交易事实摘要必须为 32 字节。", parameterName);
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
