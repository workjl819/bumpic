using Microsoft.Extensions.Options;
using Bumpic.Domain;

namespace Bumpic.Web.Options;

/// <summary>
/// 赠送点数配置。
/// </summary>
public class RewardPointsOptions
{
    /// <summary>
    /// 注册赠点点数（每个账户发放一次；账户注销后重新注册的新账户会重新发放）。
    /// 配置节 <c>RewardPoints:Registration</c>，默认 5 点。
    /// </summary>
    public int Registration { get; set; } = 5;

    /// <summary>
    /// 邀请奖励点数；建立邀请关系时发放给邀请人。
    /// </summary>
    public int Invitation { get; set; } = InvitationPolicy.RewardPoints;

}

/// <summary>
/// 赠送点数配置验证器。
/// </summary>
public class RewardPointsOptionsValidator : IValidateOptions<RewardPointsOptions>
{
    /// <summary>
    /// 校验各奖励点数取值。
    /// </summary>
    /// <param name="name">配置名称。</param>
    /// <param name="options">待验证配置。</param>
    public ValidateOptionsResult Validate(string? name, RewardPointsOptions options)
    {
        if (options.Registration <= 0 || options.Registration > 1000)
        {
            return ValidateOptionsResult.Fail("RewardPoints:Registration 必须在 1 到 1000 之间。");
        }

        if (options.Invitation <= 0 || options.Invitation > 1000)
        {
            return ValidateOptionsResult.Fail("RewardPoints:Invitation 必须在 1 到 1000 之间。");
        }

        return ValidateOptionsResult.Success;
    }
}
