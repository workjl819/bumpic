using System.Security.Cryptography;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;

/// <summary>
/// 幂等请求标识。
/// </summary>
public partial record IdempotentRequestId : IGuidStronglyTypedId;

/// <summary>
/// 幂等请求聚合根。
/// </summary>
public class IdempotentRequest : Entity<IdempotentRequestId>, IAggregateRoot
{
    /// <summary>
    /// 供 EF Core 使用的构造函数。
    /// </summary>
    protected IdempotentRequest()
    {
    }

    /// <summary>
    /// 创建并领取新的幂等请求。
    /// </summary>
    public IdempotentRequest(
        UserAccountId userAccountId,
        string operation,
        string idempotencyKey,
        byte[] requestHash,
        int requestHashVersion,
        Guid leaseToken,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset now)
    {
        ValidateIdentity(
            operation: operation,
            idempotencyKey: idempotencyKey,
            requestHash: requestHash,
            requestHashVersion: requestHashVersion,
            leaseToken: leaseToken,
            leaseExpiresAt: leaseExpiresAt,
            now: now);

        UserAccountId = userAccountId;
        Operation = operation.Trim();
        IdempotencyKey = idempotencyKey.Trim();
        RequestHash = requestHash.ToArray();
        RequestHashVersion = requestHashVersion;
        Status = IdempotentRequestStatus.Processing;
        LeaseToken = leaseToken;
        LeaseExpiresAt = leaseExpiresAt;
        AttemptCount = 1;
        LastAttemptAt = now;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// 发起请求的用户标识。
    /// </summary>
    public UserAccountId UserAccountId { get; private set; } = new(Guid.Empty);

    /// <summary>
    /// 幂等操作名称。
    /// </summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>
    /// 客户端幂等键。
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>
    /// 规范化请求摘要。
    /// </summary>
    public byte[] RequestHash { get; private set; } = [];

    /// <summary>
    /// 请求摘要算法版本。
    /// </summary>
    public int RequestHashVersion { get; private set; }

    /// <summary>
    /// 已关联的内部商店交易标识。
    /// </summary>
    public StoreTransactionId? StoreTransactionId { get; private set; }

    /// <summary>
    /// 当前处理状态。
    /// </summary>
    public IdempotentRequestStatus Status { get; private set; }

    /// <summary>
    /// 当前执行者持有的 fencing 令牌。
    /// </summary>
    public Guid? LeaseToken { get; private set; }

    /// <summary>
    /// 当前执行租约截止时间。
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    /// <summary>
    /// 成功领取执行权的次数。
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// 可重试失败最早再次领取时间。
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; private set; }

    /// <summary>
    /// 最近一次稳定或诊断结果码。
    /// </summary>
    public string? ResultCode { get; private set; }

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// 最近一次领取执行权时间。
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// 最近更新时间。
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
    /// 尝试领取或接管请求执行权。
    /// </summary>
    public IdempotentRequestAcquireResult TryAcquire(
        byte[] requestHash,
        int requestHashVersion,
        Guid newLeaseToken,
        TimeSpan leaseDuration,
        DateTimeOffset now)
    {
        if (requestHashVersion != RequestHashVersion || !HashesEqual(requestHash: requestHash))
        {
            return IdempotentRequestAcquireResult.RequestHashMismatch;
        }

        if (Status == IdempotentRequestStatus.Completed)
        {
            return IdempotentRequestAcquireResult.Completed;
        }

        if (Status == IdempotentRequestStatus.Processing
            && LeaseExpiresAt.HasValue
            && LeaseExpiresAt.Value > now)
        {
            return IdempotentRequestAcquireResult.Processing;
        }

        if (Status == IdempotentRequestStatus.RetryableFailed
            && NextRetryAt.HasValue
            && NextRetryAt.Value > now)
        {
            return IdempotentRequestAcquireResult.RetryNotDue;
        }

        if (newLeaseToken == Guid.Empty || leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException("新租约令牌和时长必须有效。");
        }

        Status = IdempotentRequestStatus.Processing;
        LeaseToken = newLeaseToken;
        LeaseExpiresAt = now.Add(leaseDuration);
        NextRetryAt = null;
        AttemptCount++;
        LastAttemptAt = now;
        UpdatedAt = now;
        return IdempotentRequestAcquireResult.Acquired;
    }

    /// <summary>
    /// 使用当前 fencing 令牌完成请求。
    /// </summary>
    public void Complete(
        Guid leaseToken,
        string resultCode,
        StoreTransactionId? storeTransactionId,
        DateTimeOffset now)
    {
        EnsureLeaseOwner(leaseToken: leaseToken, now: now);
        if (string.IsNullOrWhiteSpace(resultCode))
        {
            throw new ArgumentException("完成结果码不能为空。", nameof(resultCode));
        }

        Status = IdempotentRequestStatus.Completed;
        ResultCode = resultCode.Trim();
        StoreTransactionId = storeTransactionId;
        CompletedAt = now;
        LeaseToken = null;
        LeaseExpiresAt = null;
        NextRetryAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 使用当前 fencing 令牌记录可重试失败。
    /// </summary>
    public void MarkRetryableFailed(
        Guid leaseToken,
        string resultCode,
        DateTimeOffset nextRetryAt,
        DateTimeOffset now)
    {
        EnsureLeaseOwner(leaseToken: leaseToken, now: now);
        if (string.IsNullOrWhiteSpace(resultCode) || nextRetryAt <= now)
        {
            throw new ArgumentException("可重试失败必须包含结果码和未来重试时间。");
        }

        Status = IdempotentRequestStatus.RetryableFailed;
        ResultCode = resultCode.Trim();
        NextRetryAt = nextRetryAt;
        LeaseToken = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 使用当前请求摘要进行固定时间比较。
    /// </summary>
    public bool HashesEqual(byte[] requestHash)
    {
        return requestHash.Length == RequestHash.Length
               && CryptographicOperations.FixedTimeEquals(requestHash, RequestHash);
    }

    private static void ValidateIdentity(
        string operation,
        string idempotencyKey,
        byte[] requestHash,
        int requestHashVersion,
        Guid leaseToken,
        DateTimeOffset leaseExpiresAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("操作名称和幂等键不能为空。");
        }

        if (requestHash.Length != 32 || requestHashVersion <= 0)
        {
            throw new ArgumentException("请求摘要或摘要版本不合法。");
        }

        if (leaseToken == Guid.Empty || leaseExpiresAt <= now)
        {
            throw new ArgumentException("初始执行租约不合法。");
        }
    }

    private void EnsureLeaseOwner(Guid leaseToken, DateTimeOffset now)
    {
        if (Status != IdempotentRequestStatus.Processing
            || LeaseToken != leaseToken
            || !LeaseExpiresAt.HasValue
            || LeaseExpiresAt.Value <= now)
        {
            throw new InvalidOperationException("当前执行者不持有有效幂等请求租约。");
        }
    }
}
