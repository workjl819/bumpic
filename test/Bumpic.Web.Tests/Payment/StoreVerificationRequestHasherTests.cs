using System.Text;
using Bumpic.Domain.Enums;
using Bumpic.Web.Services.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 商店验单幂等请求摘要测试。
/// </summary>
public class StoreVerificationRequestHasherTests
{
    private readonly StoreVerificationRequestHasher _hasher = new();

    /// <summary>
    /// 平台交易标识改用原始值后摘要规范版本递增。
    /// </summary>
    [Fact]
    public void Version_Should_Be_Incremented_For_Plain_Transaction_Identifier()
    {
        Assert.Equal(2, _hasher.Version);
    }

    /// <summary>
    /// 相同请求产生相同摘要。
    /// </summary>
    [Fact]
    public void ComputeRequestHash_Should_Be_Deterministic()
    {
        var evidenceHash = ComputeEvidenceHash("evidence");

        var first = ComputeRequestHash(evidenceHash: evidenceHash);
        var second = ComputeRequestHash(evidenceHash: evidenceHash);

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// 平台交易标识、商店或商品变化都会产生不同摘要。
    /// </summary>
    [Fact]
    public void ComputeRequestHash_Should_Separate_Transaction_Identity()
    {
        var evidenceHash = ComputeEvidenceHash("evidence");

        var baseline = ComputeRequestHash(evidenceHash: evidenceHash);
        var otherTransaction = _hasher.ComputeRequestHash(
            store: AppStore.GooglePlay,
            productId: "photorescue.points.small",
            externalTransactionId: "other-purchase-token",
            evidenceHash: evidenceHash,
            externalAccountToken: "account-hash");
        var otherStore = _hasher.ComputeRequestHash(
            store: AppStore.AppleAppStore,
            productId: "photorescue.points.small",
            externalTransactionId: "purchase-token",
            evidenceHash: evidenceHash,
            externalAccountToken: "account-hash");

        Assert.NotEqual(baseline, otherTransaction);
        Assert.NotEqual(baseline, otherStore);
    }

    /// <summary>
    /// 验单证据摘要为 32 字节且对原文敏感。
    /// </summary>
    [Fact]
    public void ComputeEvidenceHash_Should_Hash_Evidence()
    {
        var first = _hasher.ComputeEvidenceHash("evidence");
        var second = _hasher.ComputeEvidenceHash("other-evidence");

        Assert.Equal(32, first.Length);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 缺少平台交易标识或证据摘要长度非法时拒绝计算。
    /// </summary>
    [Fact]
    public void ComputeRequestHash_Should_Reject_Invalid_Arguments()
    {
        Assert.Throws<ArgumentException>(() => _hasher.ComputeRequestHash(
            store: AppStore.GooglePlay,
            productId: "photorescue.points.small",
            externalTransactionId: " ",
            evidenceHash: ComputeEvidenceHash("evidence"),
            externalAccountToken: null));
        Assert.Throws<ArgumentException>(() => _hasher.ComputeRequestHash(
            store: AppStore.GooglePlay,
            productId: "photorescue.points.small",
            externalTransactionId: "purchase-token",
            evidenceHash: new byte[16],
            externalAccountToken: null));
    }

    private byte[] ComputeRequestHash(byte[] evidenceHash)
    {
        return _hasher.ComputeRequestHash(
            store: AppStore.GooglePlay,
            productId: "photorescue.points.small",
            externalTransactionId: "purchase-token",
            evidenceHash: evidenceHash,
            externalAccountToken: "account-hash");
    }

    private byte[] ComputeEvidenceHash(string evidence)
    {
        return _hasher.ComputeEvidenceHash(evidence: evidence);
    }
}
