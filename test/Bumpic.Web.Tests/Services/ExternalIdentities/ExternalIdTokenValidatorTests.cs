using System.Security.Cryptography;
using System.Text;
using NetCorePal.Extensions.Primitives;
using Bumpic.Web.Services.ExternalIdentities;

namespace Bumpic.Web.Tests.Services.ExternalIdentities;

/// <summary>
/// ID Token 校验核心单元测试（ES256/RS256、iss、aud、exp、kid、畸形输入）。
/// </summary>
public class ExternalIdTokenValidatorTests
{
    private const string Issuer = "accounts.google.com";
    private const string Audience = "client-id-123";

    /// <summary>
    /// ES256 合法令牌通过并返回声明。
    /// </summary>
    [Fact]
    public void Validate_ValidEs256Token_ReturnsClaims()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry(), email: "u@example.com");

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.Equal("subject-1", claims.Subject);
        Assert.Equal("u@example.com", claims.Email);
    }

    /// <summary>
    /// RS256 合法令牌通过（Google 使用 RSA 签名）。
    /// </summary>
    [Fact]
    public void Validate_ValidRs256Token_ReturnsClaims()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = CreateRs256Token(key, exp: FutureExpiry());

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.Equal("subject-1", claims.Subject);
    }

    /// <summary>
    /// 畸形 Base64Url / JSON 输入返回业务错误码而不是抛出底层异常。
    /// </summary>
    [Theory]
    [InlineData("not-a-jws")]
    [InlineData("!!!.payload.sig")]
    [InlineData("a.b.c")]
    public void Validate_MalformedToken_ThrowsKnownException(string token)
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));

        Assert.Equal(ExternalIdTokenErrorCodes.Malformed, exception.Message);
    }

    /// <summary>
    /// 缺少 exp 的令牌被拒绝。
    /// </summary>
    [Fact]
    public void Validate_MissingExp_Throws()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: null);

        Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));
    }

    /// <summary>
    /// 刚过期但在时钟容差内的令牌仍通过。
    /// </summary>
    [Fact]
    public void Validate_ExpiredWithinClockSkew_Passes()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeSeconds());

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.Equal("subject-1", claims.Subject);
    }

    /// <summary>
    /// 超出时钟容差的过期令牌被拒绝。
    /// </summary>
    [Fact]
    public void Validate_ExpiredBeyondClockSkew_Throws()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds());

        Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));
    }

    /// <summary>
    /// Google 允许的两种 issuer 写法都能通过。
    /// </summary>
    [Theory]
    [InlineData("accounts.google.com")]
    [InlineData("https://accounts.google.com")]
    public void Validate_GoogleIssuerVariants_Pass(string issuer)
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry(), issuer: issuer);
        var requirements = new IdTokenValidationRequirements(
            new[] { "accounts.google.com", "https://accounts.google.com" },
            new[] { Audience },
            new[] { "ES256", "RS256" },
            ExpectedNonce: null);

        var claims = ExternalIdTokenValidator.Validate(token, jwks, requirements);

        Assert.Equal("subject-1", claims.Subject);
    }

    /// <summary>
    /// aud 为数组且包含允许值时通过。
    /// </summary>
    [Fact]
    public void Validate_AudienceArrayContainingAllowed_Passes()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry(), audience: new[] { "other", Audience });

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.Equal("subject-1", claims.Subject);
    }

    /// <summary>
    /// kid 缺失时返回内部 key-not-found 码，供上层刷新 JWKS。
    /// </summary>
    [Fact]
    public void Validate_MissingKid_ThrowsKeyNotFound()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry(), omitKid: true);

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));

        Assert.Equal(ExternalIdTokenErrorCodes.KeyNotFound, exception.Message);
    }

    /// <summary>
    /// JWKS 中不存在对应 kid 时返回内部 key-not-found 码。
    /// </summary>
    [Fact]
    public void Validate_KidNotFound_ThrowsKeyNotFound()
    {
        using var key = TestIdTokenFactory.CreateEcKey("token-kid", out _);
        using var otherKey = TestIdTokenFactory.CreateEcKey("jwks-kid", out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry(), keyId: "token-kid");

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));

        Assert.Equal(ExternalIdTokenErrorCodes.KeyNotFound, exception.Message);
    }

    /// <summary>
    /// 令牌算法不在白名单（如 HS256）时拒绝。
    /// </summary>
    [Fact]
    public void Validate_AlgorithmNotAllowed_Throws()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry(), algorithm: "HS256");

        Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));
    }

    /// <summary>
    /// kid 相同但密钥类型不匹配（ES256 令牌遇到 RSA 密钥）时视为未命中。
    /// </summary>
    [Fact]
    public void Validate_KeyTypeMismatch_ThrowsKeyNotFound()
    {
        using var ecKey = TestIdTokenFactory.CreateEcKey("shared-kid", out _);
        using var rsaKey = TestIdTokenFactory.CreateRsaKey("shared-kid", out var rsaJwks);
        var token = CreateEs256Token(ecKey, exp: FutureExpiry(), keyId: "shared-kid");

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, rsaJwks, Requirements()));

        Assert.Equal(ExternalIdTokenErrorCodes.KeyNotFound, exception.Message);
    }

    /// <summary>
    /// 签名被篡改时拒绝。
    /// </summary>
    [Fact]
    public void Validate_TamperedSignature_Throws()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = CreateEs256Token(key, exp: FutureExpiry());
        var tampered = token[..^2] + (token[^1] == 'A' ? "B" : "A");

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(tampered, jwks, Requirements()));

        Assert.Equal(ExternalIdTokenErrorCodes.SignatureInvalid, exception.Message);
    }

    /// <summary>
    /// 受众不匹配时给出可诊断的内部错误码，并在其中带上令牌实际的 aud，便于与配置白名单比对。
    /// </summary>
    [Fact]
    public void Validate_AudienceMismatch_ReportsActualAudience()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", Issuer, "web-client-id.apps.googleusercontent.com",
            FutureExpiry(), "subject-1");

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));

        Assert.StartsWith(ExternalIdTokenErrorCodes.AudienceMismatch, exception.Message);
        Assert.Contains("web-client-id.apps.googleusercontent.com", exception.Message);
    }

    /// <summary>
    /// 签发方不匹配时给出可诊断的内部错误码并带上实际 iss。
    /// </summary>
    [Fact]
    public void Validate_IssuerMismatch_ReportsActualIssuer()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", "https://evil.example.com", Audience, FutureExpiry(), "subject-1");

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));

        Assert.StartsWith(ExternalIdTokenErrorCodes.IssuerMismatch, exception.Message);
        Assert.Contains("evil.example.com", exception.Message);
    }

    /// <summary>
    /// 令牌过期时给出可诊断的内部错误码。
    /// </summary>
    [Fact]
    public void Validate_Expired_ReportsExpiry()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", Issuer, Audience,
            DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(), "subject-1");

        var exception = Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, Requirements()));

        Assert.StartsWith(ExternalIdTokenErrorCodes.Expired, exception.Message);
    }

    /// <summary>
    /// Apple 语义：客户端传原始 nonce、令牌里是其 SHA-256 摘要时，开启摘要比对后通过。
    /// </summary>
    [Fact]
    public void Validate_AppleHashedNonce_MatchesRawNonce()
    {
        const string rawNonce = "raw-nonce-abc";
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.EcKeyId, "ES256", Issuer, Audience, FutureExpiry(), "subject-1",
            nonce: Sha256Hex(rawNonce));
        var requirements = new IdTokenValidationRequirements(
            new[] { Issuer },
            new[] { Audience },
            new[] { "ES256", "RS256" },
            ExpectedNonce: rawNonce,
            AllowHashedNonce: true);

        var claims = ExternalIdTokenValidator.Validate(token, jwks, requirements);

        Assert.Equal(Sha256Hex(rawNonce), claims.Nonce);
    }

    /// <summary>
    /// Google 语义：未开启摘要比对时，令牌中的摘要与客户端传入的 nonce 不相等即拒绝。
    /// </summary>
    [Fact]
    public void Validate_HashedNonceWithFlagDisabled_Throws()
    {
        const string rawNonce = "raw-nonce-abc";
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.EcKeyId, "ES256", Issuer, Audience, FutureExpiry(), "subject-1",
            nonce: Sha256Hex(rawNonce));
        var requirements = new IdTokenValidationRequirements(
            new[] { Issuer },
            new[] { Audience },
            new[] { "ES256", "RS256" },
            ExpectedNonce: rawNonce);

        Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, requirements));
    }

    /// <summary>
    /// nonce 完全不匹配时拒绝。
    /// </summary>
    [Fact]
    public void Validate_NonceMismatch_Throws()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.EcKeyId, "ES256", Issuer, Audience, FutureExpiry(), "subject-1",
            nonce: "another-nonce");
        var requirements = new IdTokenValidationRequirements(
            new[] { Issuer },
            new[] { Audience },
            new[] { "ES256", "RS256" },
            ExpectedNonce: "raw-nonce-abc",
            AllowHashedNonce: true);

        Assert.Throws<KnownException>(() => ExternalIdTokenValidator.Validate(token, jwks, requirements));
    }

    /// <summary>
    /// Google 语义：email_verified 为布尔 true 时标记邮箱已验证。
    /// </summary>
    [Fact]
    public void Validate_EmailVerifiedBoolean_MarksEmailVerified()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", Issuer, Audience, FutureExpiry(), "subject-1",
            email: "u@example.com", emailVerified: true);

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.True(claims.EmailVerified);
        Assert.Equal("u@example.com", claims.Email);
    }

    /// <summary>
    /// Apple 语义：email_verified 为字符串 "true" 时同样标记邮箱已验证。
    /// </summary>
    [Fact]
    public void Validate_EmailVerifiedString_MarksEmailVerified()
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.EcKeyId, "ES256", Issuer, Audience, FutureExpiry(), "subject-1",
            email: "u@privaterelay.appleid.com", emailVerified: "true");

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.True(claims.EmailVerified);
    }

    /// <summary>
    /// email_verified 缺失或为 false 时不得信任令牌邮箱。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData("false")]
    public void Validate_EmailNotVerified_DoesNotTrustEmail(object? emailVerified)
    {
        using var key = TestIdTokenFactory.CreateEcKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.EcKeyId, "ES256", Issuer, Audience, FutureExpiry(), "subject-1",
            email: "u@example.com", emailVerified: emailVerified);

        var claims = ExternalIdTokenValidator.Validate(token, jwks, Requirements());

        Assert.False(claims.EmailVerified);
    }

    /// <summary>
    /// 默认校验要求（ES256/RS256，任一签发方）。
    /// </summary>
    private static IdTokenValidationRequirements Requirements()
    {
        return new IdTokenValidationRequirements(
            AllowedIssuers: new[] { Issuer },
            AllowedAudiences: new[] { Audience },
            AllowedAlgorithms: new[] { "ES256", "RS256" },
            ExpectedNonce: null);
    }

    /// <summary>
    /// 计算字符串的 SHA-256 十六进制摘要（与 Apple SDK 写入令牌的形式一致）。
    /// </summary>
    private static string Sha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// 一小时后的过期时间。
    /// </summary>
    private static long FutureExpiry()
    {
        return DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    }

    /// <summary>
    /// 构造 ES256 令牌（默认 kid 为测试 EC kid）。
    /// </summary>
    private static string CreateEs256Token(
        ECDsa key,
        long? exp,
        string? email = null,
        string issuer = Issuer,
        object? audience = null,
        string keyId = TestIdTokenFactory.EcKeyId,
        bool omitKid = false,
        string algorithm = "ES256")
    {
        return TestIdTokenFactory.CreateToken(
            key,
            omitKid ? string.Empty : keyId,
            algorithm,
            issuer,
            audience ?? Audience,
            exp,
            "subject-1",
            email: email);
    }

    /// <summary>
    /// 构造 RS256 令牌。
    /// </summary>
    private static string CreateRs256Token(RSA key, long? exp)
    {
        return TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", Issuer, Audience, exp, "subject-1");
    }
}
