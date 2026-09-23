using Bumpic.Domain.AggregateModel.IdempotentRequestAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.Tests;

/// <summary>
/// 幂等请求领域测试。
/// </summary>
public class IdempotentRequestTests
{
    /// <summary>
    /// 进程崩溃留下的过期 Processing 租约可以由新执行者接管。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Take_Over_Expired_Processing_Lease()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now: now, leaseDuration: TimeSpan.FromMinutes(1));
        var newLeaseToken = Guid.NewGuid();

        var result = request.TryAcquire(
            requestHash: new byte[32],
            requestHashVersion: 1,
            newLeaseToken: newLeaseToken,
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Equal(IdempotentRequestAcquireResult.Acquired, result);
        Assert.Equal(newLeaseToken, request.LeaseToken);
        Assert.Equal(2, request.AttemptCount);
    }

    /// <summary>
    /// 并发重复请求在有效租约期间不能取得执行权。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Return_Processing_When_Lease_Is_Active()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now: now, leaseDuration: TimeSpan.FromMinutes(1));

        var result = request.TryAcquire(
            requestHash: new byte[32],
            requestHashVersion: 1,
            newLeaseToken: Guid.NewGuid(),
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddSeconds(30));

        Assert.Equal(IdempotentRequestAcquireResult.Processing, result);
        Assert.Equal(1, request.AttemptCount);
    }

    /// <summary>
    /// 临时失败到达退避时间后允许同键同负载重新领取。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Retry_After_Retryable_Failure_Backoff()
    {
        var now = DateTimeOffset.UtcNow;
        var leaseToken = Guid.NewGuid();
        var request = CreateRequest(
            now: now,
            leaseDuration: TimeSpan.FromMinutes(1),
            leaseToken: leaseToken);
        request.MarkRetryableFailed(
            leaseToken: leaseToken,
            resultCode: "STORE_SERVICE_UNAVAILABLE",
            nextRetryAt: now.AddMinutes(2),
            now: now.AddSeconds(10));

        var earlyResult = request.TryAcquire(
            requestHash: new byte[32],
            requestHashVersion: 1,
            newLeaseToken: Guid.NewGuid(),
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(1));
        var dueResult = request.TryAcquire(
            requestHash: new byte[32],
            requestHashVersion: 1,
            newLeaseToken: Guid.NewGuid(),
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Equal(IdempotentRequestAcquireResult.RetryNotDue, earlyResult);
        Assert.Equal(IdempotentRequestAcquireResult.Acquired, dueResult);
        Assert.Equal(2, request.AttemptCount);
    }

    /// <summary>
    /// 相同幂等键使用不同请求摘要时始终拒绝执行。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Reject_Different_Request_Hash()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now: now, leaseDuration: TimeSpan.FromMinutes(1));
        var differentHash = new byte[32];
        differentHash[0] = 1;

        var result = request.TryAcquire(
            requestHash: differentHash,
            requestHashVersion: 1,
            newLeaseToken: Guid.NewGuid(),
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Equal(IdempotentRequestAcquireResult.RequestHashMismatch, result);
        Assert.Equal(1, request.AttemptCount);
    }

    /// <summary>
    /// 相同摘要字节但摘要算法版本不同时拒绝复用幂等键。
    /// </summary>
    [Fact]
    public void TryAcquire_Should_Reject_Different_Request_Hash_Version()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now: now, leaseDuration: TimeSpan.FromMinutes(1));

        var result = request.TryAcquire(
            requestHash: new byte[32],
            requestHashVersion: 2,
            newLeaseToken: Guid.NewGuid(),
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Equal(IdempotentRequestAcquireResult.RequestHashMismatch, result);
        Assert.Equal(1, request.AttemptCount);
    }

    /// <summary>
    /// 旧租约令牌不能完成已被接管的请求。
    /// </summary>
    [Fact]
    public void Complete_Should_Reject_Stale_Fencing_Token()
    {
        var now = DateTimeOffset.UtcNow;
        var oldLeaseToken = Guid.NewGuid();
        var request = CreateRequest(
            now: now,
            leaseDuration: TimeSpan.FromMinutes(1),
            leaseToken: oldLeaseToken);
        var newLeaseToken = Guid.NewGuid();
        request.TryAcquire(
            requestHash: new byte[32],
            requestHashVersion: 1,
            newLeaseToken: newLeaseToken,
            leaseDuration: TimeSpan.FromMinutes(1),
            now: now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => request.Complete(
            leaseToken: oldLeaseToken,
            resultCode: "OK",
            storeTransactionId: new StoreTransactionId(Guid.NewGuid()),
            now: now.AddMinutes(2).AddSeconds(1)));
    }

    private static IdempotentRequest CreateRequest(
        DateTimeOffset now,
        TimeSpan leaseDuration,
        Guid? leaseToken = null)
    {
        return new IdempotentRequest(
            userAccountId: new UserAccountId(Guid.NewGuid()),
            operation: "VerifyStorePurchase",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            requestHash: new byte[32],
            requestHashVersion: 1,
            leaseToken: leaseToken ?? Guid.NewGuid(),
            leaseExpiresAt: now.Add(leaseDuration),
            now: now);
    }
}
