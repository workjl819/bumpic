using Bumpic.Web.Services.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google Play 待消费退避策略测试。
/// </summary>
public class StoreConsumptionRetryPolicyTests
{
    /// <summary>
    /// 临时失败采用指数退避且最大不超过一小时。
    /// </summary>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 60)]
    [InlineData(7, 3600)]
    [InlineData(20, 3600)]
    public void CalculateDelay_RetryableFailure_UsesCappedExponentialBackoff(
        int completedAttemptCount,
        int expectedSeconds)
    {
        var delay = StoreConsumptionRetryPolicy.CalculateDelay(
            completedAttemptCount: completedAttemptCount,
            isRetryable: true);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    /// <summary>
    /// 确定性失败停止高频重试并改为二十四小时低频复查。
    /// </summary>
    [Fact]
    public void CalculateDelay_DeterministicFailure_UsesDailyRecheck()
    {
        var delay = StoreConsumptionRetryPolicy.CalculateDelay(
            completedAttemptCount: 0,
            isRetryable: false);

        Assert.Equal(TimeSpan.FromHours(24), delay);
    }
}
