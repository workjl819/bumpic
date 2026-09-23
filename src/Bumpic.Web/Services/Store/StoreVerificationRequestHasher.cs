using System.Security.Cryptography;
using System.Text;
using Bumpic.Domain.Enums;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Services.Store;

/// <summary>
/// 使用固定字段顺序计算商店验单幂等请求摘要。
/// </summary>
public class StoreVerificationRequestHasher : IStoreVerificationRequestHasher
{
    /// <summary>
    /// 摘要规范版本；平台交易查询键摘要改为原始交易标识后递增。
    /// </summary>
    private const int RequestHashVersion = 2;

    /// <inheritdoc />
    public int Version => RequestHashVersion;

    /// <inheritdoc />
    public byte[] ComputeEvidenceHash(string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        return SHA256.HashData(Encoding.UTF8.GetBytes(evidence));
    }

    /// <inheritdoc />
    public byte[] ComputeRequestHash(
        AppStore store,
        string productId,
        string externalTransactionId,
        byte[] evidenceHash,
        string? externalAccountToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalTransactionId);
        if (evidenceHash.Length != 32)
        {
            throw new ArgumentException("验单证据摘要必须为 32 字节。", nameof(evidenceHash));
        }

        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, RequestHashVersion);
        CanonicalEncoding.WriteString(payload, store.ToString());
        CanonicalEncoding.WriteString(payload, productId.Trim());
        CanonicalEncoding.WriteString(payload, externalTransactionId.Trim());
        CanonicalEncoding.WriteBytes(payload, evidenceHash);
        CanonicalEncoding.WriteNullableString(payload, externalAccountToken?.Trim());
        return SHA256.HashData(payload.ToArray());
    }
}
