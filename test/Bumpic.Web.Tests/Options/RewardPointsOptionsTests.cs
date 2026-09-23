using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Options;

/// <summary>
/// 赠送点数配置默认值与校验单元测试。
/// </summary>
public class RewardPointsOptionsTests
{
    /// <summary>
    /// 默认注册赠点与邀请奖励均为 5 点。
    /// </summary>
    [Fact]
    public void Defaults_AreBothFive()
    {
        var options = new RewardPointsOptions();

        Assert.Equal(5, options.Registration);
        Assert.Equal(5, options.Invitation);
    }

    /// <summary>
    /// 非正数或超过上限的配置会被拒绝。
    /// </summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(1001, 5)]
    [InlineData(5, 1001)]
    public void Validate_OutOfRange_Fails(int registration, int invitation)
    {
        var validator = new RewardPointsOptionsValidator();

        var result = validator.Validate(null, new RewardPointsOptions
        {
            Registration = registration,
            Invitation = invitation
        });

        Assert.True(result.Failed);
    }

    /// <summary>
    /// 正常范围内的配置通过校验。
    /// </summary>
    [Fact]
    public void Validate_InRange_Succeeds()
    {
        var validator = new RewardPointsOptionsValidator();

        var result = validator.Validate(null, new RewardPointsOptions { Registration = 5, Invitation = 5 });

        Assert.False(result.Failed);
    }
}
