using System.Text;
using System.Text.Json;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 测试用 Apple 签名交易生成器。
/// </summary>
internal static class TestAppleTransactionSigner
{
    internal const string DefaultBundleId = "com.lumavill.photorescue.dev";
    internal const string DefaultProductId = "com.lumavill.photorescue.dev.credits100";
    internal const string DefaultTransactionId = "2000000123456789";
    internal const string SandboxEnvironment = "Sandbox";

    /// <summary>
    /// 生成使用测试证书链签名的 Apple 交易 JWS。
    /// </summary>
    internal static (string Jws, AppleStoreOptions Options, DateTimeOffset SignedDate) Sign(
        string bundleId,
        string productId,
        string transactionId,
        string appAccountToken,
        string environment,
        string deploymentBundleId = DefaultBundleId)
    {
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        return (
            SignWithChain(
                chain: chain,
                bundleId: bundleId,
                productId: productId,
                transactionId: transactionId,
                appAccountToken: appAccountToken,
                environment: environment,
                signedDate: now),
            CreateOptions(chain: chain, bundleId: deploymentBundleId, environment: environment),
            now);
    }

    /// <summary>
    /// 使用指定证书链生成 Apple 交易 JWS，便于让客户端与服务端交易共用同一信任根。
    /// </summary>
    internal static string SignWithChain(
        TestAppleCertificateChain chain,
        string bundleId,
        string productId,
        string transactionId,
        string appAccountToken,
        string environment = SandboxEnvironment,
        DateTimeOffset? signedDate = null,
        long revocationDate = 0,
        string transactionType = "Consumable",
        int quantity = 1)
    {
        var now = signedDate ?? DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["alg"] = "ES256",
            ["x5c"] = chain.BuildX5c()
        });
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["transactionId"] = transactionId,
            ["originalTransactionId"] = transactionId,
            ["bundleId"] = bundleId,
            ["productId"] = productId,
            ["type"] = transactionType,
            ["quantity"] = quantity,
            ["purchaseDate"] = now.AddMinutes(-1).ToUnixTimeMilliseconds(),
            ["signedDate"] = now.ToUnixTimeMilliseconds(),
            ["environment"] = environment,
            ["appAccountToken"] = appAccountToken,
            ["price"] = 4990,
            ["currency"] = "USD",
            ["revocationDate"] = revocationDate
        });
        var signingInput = $"{TestAppleCertificateChain.EncodeBase64Url(Encoding.UTF8.GetBytes(header))}."
                           + TestAppleCertificateChain.EncodeBase64Url(Encoding.UTF8.GetBytes(payload));
        return $"{signingInput}.{chain.Sign(signingInput: signingInput)}";
    }

    /// <summary>
    /// 构建信任指定测试证书链的部署配置。
    /// </summary>
    internal static AppleStoreOptions CreateOptions(
        TestAppleCertificateChain chain,
        string bundleId = DefaultBundleId,
        string environment = SandboxEnvironment)
    {
        return new AppleStoreOptions
        {
            BundleId = bundleId,
            Environment = environment,
            EnableOnlineChecks = false,
            RootCertificatesPem = [chain.RootCertificate.ExportCertificatePem()]
        };
    }

    /// <summary>
    /// 生成无填充 Base64Url 字符串。
    /// </summary>
    internal static string EncodeBase64Url(byte[] value)
    {
        return TestAppleCertificateChain.EncodeBase64Url(value: value);
    }
}
