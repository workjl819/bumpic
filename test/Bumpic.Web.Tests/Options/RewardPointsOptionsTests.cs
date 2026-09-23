using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Options;

/// <summary>
/// 注册赠点配置测试。
/// </summary>
public class RewardPointsOptionsTests
{
    /// <summary>
    /// 默认注册赠点为五点。
    /// </summary>
    [Fact]
    public void Defaults_RegistrationIsFive()
    {
        var options = new RewardPointsOptions();

        Assert.Equal(5, options.Registration);
    }

    /// <summary>
    /// 非正数或超过上限的注册赠点会被拒绝。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Validate_OutOfRange_Fails(int registration)
    {
        var validator = new RewardPointsOptionsValidator();

        var result = validator.Validate(null, new RewardPointsOptions { Registration = registration });

        Assert.True(result.Failed);
    }

    /// <summary>
    /// 合法的注册赠点配置通过校验。
    /// </summary>
    [Fact]
    public void Validate_InRange_Succeeds()
    {
        var validator = new RewardPointsOptionsValidator();

        var result = validator.Validate(null, new RewardPointsOptions { Registration = 5 });

        Assert.False(result.Failed);
    }
}
