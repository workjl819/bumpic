namespace Bumpic.Domain.Enums;

/// <summary>
/// 幂等请求执行权领取结果。
/// </summary>
public enum IdempotentRequestAcquireResult
{
    Acquired,
    Processing,
    Completed,
    RetryNotDue,
    RequestHashMismatch
}
