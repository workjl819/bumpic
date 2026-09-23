namespace Bumpic.Domain.Enums;

/// <summary>
/// 幂等请求处理状态。
/// </summary>
public enum IdempotentRequestStatus
{
    Processing,
    Completed,
    RetryableFailed
}
