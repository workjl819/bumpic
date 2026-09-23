using System.Text;
using System.Text.Json;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 测试用 Apple 服务端通知 V2 签名载荷生成器。
/// </summary>
internal static class TestAppleNotificationSigner
{
    /// <summary>
    /// 生成 Apple 服务端通知 V2 的签名载荷。
    /// </summary>
    internal static (string Jws, AppleStoreOptions Options) Sign(
        string bundleId,
        string environment,
        string notificationType,
        Guid notificationUuid,
        string signedTransactionInfo,
        long signedDate,
        long? appAppleId = null,
        string deploymentBundleId = "com.lumavill.photorescue.dev",
        string deploymentEnvironment = "Sandbox")
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(signedDate);
        using var chain = TestAppleCertificateChain.Create(now: now);
        var header = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["alg"] = "ES256",
            ["x5c"] = chain.BuildX5c()
        });
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["notificationType"] = notificationType,
            ["notificationUUID"] = notificationUuid.ToString("D"),
            ["version"] = "2.0",
            ["signedDate"] = signedDate,
            ["data"] = new Dictionary<string, object?>
            {
                ["appAppleId"] = appAppleId,
                ["bundleId"] = bundleId,
                ["environment"] = environment,
                ["signedTransactionInfo"] = signedTransactionInfo
            }
        });
        var signingInput = $"{TestAppleCertificateChain.EncodeBase64Url(Encoding.UTF8.GetBytes(header))}."
                           + TestAppleCertificateChain.EncodeBase64Url(Encoding.UTF8.GetBytes(payload));
        var jws = $"{signingInput}.{chain.Sign(signingInput: signingInput)}";
        var options = new AppleStoreOptions
        {
            BundleId = deploymentBundleId,
            Environment = deploymentEnvironment,
            EnableOnlineChecks = false,
            RootCertificatesPem = [chain.RootCertificate.ExportCertificatePem()]
        };
        return (jws, options);
    }
}
