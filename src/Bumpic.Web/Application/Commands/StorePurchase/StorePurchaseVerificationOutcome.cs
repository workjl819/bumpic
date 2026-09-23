namespace Bumpic.Web.Application.Commands.StorePurchase;

/// <summary>
/// 商店验单处理结果类型。
/// </summary>
public enum StorePurchaseVerificationOutcome
{
    /// <summary>
    /// 已得到可返回给客户端的交易状态。
    /// </summary>
    Succeeded,

    /// <summary>
    /// 相同幂等请求正由其他实例处理。
    /// </summary>
    Processing,

    /// <summary>
    /// 商店服务临时不可用，可退避后重试。
    /// </summary>
    RetryableFailed,

    /// <summary>
    /// 权威结果确认请求无效，重复请求会稳定重放相同结果。
    /// </summary>
    DeterministicFailed
}
