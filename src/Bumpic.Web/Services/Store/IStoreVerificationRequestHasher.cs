using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.Store;

/// <summary>
/// 商店验单幂等请求摘要计算器。
/// </summary>
public interface IStoreVerificationRequestHasher
{
    /// <summary>
    /// 请求摘要规范版本。
    /// </summary>
    int Version { get; }

    /// <summary>
    /// 计算验单证据摘要，不保存 JWS 或购买令牌明文。
    /// </summary>
    byte[] ComputeEvidenceHash(string evidence);

    /// <summary>
    /// 计算覆盖商店、商品、平台交易标识、验单证据和账户标识的请求摘要。
    /// </summary>
    byte[] ComputeRequestHash(
        AppStore store,
        string productId,
        string externalTransactionId,
        byte[] evidenceHash,
        string? externalAccountToken);
}
