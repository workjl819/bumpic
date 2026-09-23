using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Bumpic.Web.Clients;
using Bumpic.Web.Services.ExternalIdentities;

namespace Bumpic.Web.Tests.Services.ExternalIdentities;

/// <summary>
/// 本地生成 EC(P-256)/RSA 密钥、签发 ES256/RS256 ID Token 与 JWKS 的测试工具。
/// </summary>
internal static class TestIdTokenFactory
{
    /// <summary>
    /// 默认 EC 密钥标识（Apple ES256）。
    /// </summary>
    public const string EcKeyId = "test-ec-key";

    /// <summary>
    /// 默认 RSA 密钥标识（Google RS256）。
    /// </summary>
    public const string RsaKeyId = "test-rsa-key";

    /// <summary>
    /// 生成 ES256 用 EC 密钥与对应 JWKS。
    /// </summary>
    public static ECDsa CreateEcKey(out string jwksJson)
    {
        return CreateEcKey(EcKeyId, out jwksJson);
    }

    /// <summary>
    /// 生成指定 kid 的 ES256 用 EC 密钥与对应 JWKS。
    /// </summary>
    public static ECDsa CreateEcKey(string keyId, out string jwksJson)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        jwksJson = CreateEcJwks(keyId, key);
        return key;
    }

    /// <summary>
    /// 生成 RS256 用 RSA 密钥与对应 JWKS。
    /// </summary>
    public static RSA CreateRsaKey(out string jwksJson)
    {
        return CreateRsaKey(RsaKeyId, out jwksJson);
    }

    /// <summary>
    /// 生成指定 kid 的 RS256 用 RSA 密钥与对应 JWKS。
    /// </summary>
    public static RSA CreateRsaKey(string keyId, out string jwksJson)
    {
        var key = RSA.Create(2048);
        jwksJson = CreateRsaJwks(keyId, key);
        return key;
    }

    /// <summary>
    /// 由 EC 公钥构造 JWKS。
    /// </summary>
    public static string CreateEcJwks(string keyId, ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        return JsonSerializer.Serialize(new
        {
            keys = new object[]
            {
                new
                {
                    kty = "EC",
                    kid = keyId,
                    use = "sig",
                    alg = "ES256",
                    crv = "P-256",
                    x = Base64UrlEncode(parameters.Q.X!),
                    y = Base64UrlEncode(parameters.Q.Y!)
                }
            }
        });
    }

    /// <summary>
    /// 由 RSA 公钥构造 JWKS。
    /// </summary>
    public static string CreateRsaJwks(string keyId, RSA key)
    {
        var parameters = key.ExportParameters(false);
        return JsonSerializer.Serialize(new
        {
            keys = new object[]
            {
                new
                {
                    kty = "RSA",
                    kid = keyId,
                    use = "sig",
                    alg = "RS256",
                    n = Base64UrlEncode(parameters.Modulus!),
                    e = Base64UrlEncode(parameters.Exponent!)
                }
            }
        });
    }

    /// <summary>
    /// 签发 ID Token。
    /// </summary>
    /// <param name="key">签名私钥（ECDsa 或 RSA）。</param>
    /// <param name="keyId">JWKS 中的 kid。</param>
    /// <param name="algorithm">alg（ES256 / RS256；可传其他值做负向用例）。</param>
    /// <param name="issuer">iss。</param>
    /// <param name="audience">aud（string 或 string[]）。</param>
    /// <param name="expiresAt">exp；传 null 表示不包含 exp 声明。</param>
    /// <param name="subject">sub。</param>
    /// <param name="nonce">可选 nonce。</param>
    /// <param name="email">可选 email。</param>
    /// <param name="emailVerified">可选 email_verified；Google 用布尔值，Apple 用字符串 "true"/"false"。</param>
    public static string CreateToken(
        object key,
        string keyId,
        string algorithm,
        string issuer,
        object audience,
        long? expiresAt,
        string subject,
        string? nonce = null,
        string? email = null,
        object? emailVerified = null)
    {
        var header = JsonSerializer.Serialize(new { alg = algorithm, kid = keyId });
        var payloadObject = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = subject
        };
        if (expiresAt is not null)
        {
            payloadObject["exp"] = expiresAt.Value;
        }

        if (nonce is not null)
        {
            payloadObject["nonce"] = nonce;
        }

        if (email is not null)
        {
            payloadObject["email"] = email;
        }

        if (emailVerified is not null)
        {
            payloadObject["email_verified"] = emailVerified;
        }

        var headerPart = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payloadObject)));
        var signedData = Encoding.ASCII.GetBytes($"{headerPart}.{payloadPart}");
        var signature = key switch
        {
            ECDsa ecdsa => ecdsa.SignData(signedData, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            RSA rsa => rsa.SignData(signedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            _ => throw new InvalidOperationException("仅支持 ECDsa 或 RSA 密钥")
        };
        return $"{headerPart}.{payloadPart}.{Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// Base64Url 编码。
    /// </summary>
    public static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>
/// 按调用顺序返回 JWKS 的 Apple Client 测试实现（用于验证密钥刷新重试）。
/// </summary>
internal class StubAppleAuthClient(params string[] jwksSequence) : IAppleAuthClient
{
    private int _callCount;

    /// <summary>
    /// 已调用次数。
    /// </summary>
    public int CallCount => _callCount;

    /// <inheritdoc />
    public Task<string> GetJwksAsync(CancellationToken cancellationToken)
    {
        var index = Math.Min(_callCount, jwksSequence.Length - 1);
        _callCount++;
        return Task.FromResult(jwksSequence[index]);
    }
}

/// <summary>
/// 按调用顺序返回 JWKS 的 Google Client 测试实现。
/// </summary>
internal class StubGoogleAuthClient(params string[] jwksSequence) : IGoogleAuthClient
{
    private int _callCount;

    /// <summary>
    /// 已调用次数。
    /// </summary>
    public int CallCount => _callCount;

    /// <inheritdoc />
    public Task<string> GetCertsAsync(CancellationToken cancellationToken)
    {
        var index = Math.Min(_callCount, jwksSequence.Length - 1);
        _callCount++;
        return Task.FromResult(jwksSequence[index]);
    }
}

/// <summary>
/// 用内存缓存与固定 Client 构建平台 JWKS 提供实现。
/// </summary>
internal static class TestJwksProviderFactory
{
    /// <summary>
    /// 两个平台都返回同一 JWKS。
    /// </summary>
    public static PlatformJwksProvider Create(string jwksJson)
    {
        return Create(new StubAppleAuthClient(jwksJson), new StubGoogleAuthClient(jwksJson));
    }

    /// <summary>
    /// 使用给定 Client 构建。
    /// </summary>
    public static PlatformJwksProvider Create(IAppleAuthClient appleClient, IGoogleAuthClient googleClient)
    {
        return new PlatformJwksProvider(
            new MemoryCache(new MemoryCacheOptions()),
            appleClient,
            googleClient,
            NullLogger<PlatformJwksProvider>.Instance);
    }
}
