namespace Bumpic.Web.Services.Store;

/// <summary>
/// Google Play 待消费交易的退避策略。
/// </summary>
public static class StoreConsumptionRetryPolicy
{
    private const int MaximumDelaySeconds = 3600;
    private static readonly TimeSpan DeterministicFailureRecheckDelay = TimeSpan.FromHours(24);

    /// <summary>
    /// 根据已经失败的次数计算下一次退避时间，最短三十秒、最长一小时。
    /// </summary>
    public static TimeSpan CalculateDelay(int completedAttemptCount, bool isRetryable = true)
    {
        if (!isRetryable)
        {
            return DeterministicFailureRecheckDelay;
        }

        var exponent = Math.Clamp(completedAttemptCount, 0, 7);
        var delaySeconds = Math.Min(MaximumDelaySeconds, 30 * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(delaySeconds);
    }
}
