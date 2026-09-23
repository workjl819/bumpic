using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 测试用 Apple 三级证书链（叶子、中间、根）。
/// </summary>
internal sealed record TestAppleCertificateChain(
    X509Certificate2 RootCertificate,
    X509Certificate2 IntermediateCertificate,
    X509Certificate2 LeafCertificate,
    ECDsa LeafKey) : IDisposable
{
    /// <summary>
    /// 创建一套用于本地验签的 Apple 测试证书链。
    /// </summary>
    internal static TestAppleCertificateChain Create(DateTimeOffset now)
    {
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rootRequest = new CertificateRequest(
            "CN=Bumpic Test Apple Root",
            rootKey,
            HashAlgorithmName.SHA256);
        rootRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, false, 0, true));
        var rootCertificate = rootRequest.CreateSelfSigned(
            notBefore: now.AddDays(-1),
            notAfter: now.AddDays(30));

        var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var intermediateRequest = new CertificateRequest(
            "CN=Bumpic Test Apple Intermediate",
            intermediateKey,
            HashAlgorithmName.SHA256);
        intermediateRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, false, 0, true));
        var intermediateCertificate = intermediateRequest
            .Create(
                issuerCertificate: rootCertificate,
                notBefore: now.AddDays(-1),
                notAfter: now.AddDays(25),
                serialNumber: [1, 2, 3, 4])
            .CopyWithPrivateKey(intermediateKey);
        intermediateKey.Dispose();

        var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafRequest = new CertificateRequest(
            "CN=Bumpic Test Apple Leaf",
            leafKey,
            HashAlgorithmName.SHA256);
        leafRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, false, 0, false));
        leafRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        var leafCertificate = leafRequest.Create(
            issuerCertificate: intermediateCertificate,
            notBefore: now.AddDays(-1),
            notAfter: now.AddDays(20),
            serialNumber: [1, 2, 3, 4, 5, 6, 7, 8]);

        return new TestAppleCertificateChain(
            RootCertificate: rootCertificate,
            IntermediateCertificate: intermediateCertificate,
            LeafCertificate: leafCertificate,
            LeafKey: leafKey);
    }

    /// <summary>
    /// 构造 JWS 头部的 x5c 证书链，顺序为叶子、中间、根。
    /// </summary>
    internal string[] BuildX5c()
    {
        return
        [
            Convert.ToBase64String(LeafCertificate.RawData),
            Convert.ToBase64String(IntermediateCertificate.RawData),
            Convert.ToBase64String(RootCertificate.RawData)
        ];
    }

    /// <summary>
    /// 使用叶子私钥对 JWS 签名输入生成 ES256 原始签名。
    /// </summary>
    internal string Sign(string signingInput)
    {
        var signature = LeafKey.SignData(
            data: System.Text.Encoding.ASCII.GetBytes(signingInput),
            hashAlgorithm: HashAlgorithmName.SHA256,
            signatureFormat: DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return EncodeBase64Url(value: signature);
    }

    /// <summary>
    /// 生成无填充 Base64Url 字符串。
    /// </summary>
    internal static string EncodeBase64Url(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <inheritdoc />
    public void Dispose()
    {
        LeafCertificate.Dispose();
        IntermediateCertificate.Dispose();
        RootCertificate.Dispose();
        LeafKey.Dispose();
    }
}
