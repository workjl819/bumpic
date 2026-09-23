using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NetCorePal.Extensions.Primitives;
using Bumpic.Web.Clients;
using Bumpic.Web.Services.ExternalIdentities;

namespace Bumpic.Web.Tests.Services.ExternalIdentities;

/// <summary>
/// Apple / Google 身份验证器单元测试（含 JWKS 密钥刷新重试）。
/// </summary>
public class ExternalIdentityVerifierTests
{
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string AppleAudience = "com.example.app";
    private const string GoogleIssuer = "accounts.google.com";
    private const string GoogleAudience = "client-id-123";

    /// <summary>
    /// Apple：合法 ES256 令牌 + 匹配 nonce 通过验证。
    /// </summary>
    [Fact]
    public async Task AppleVerifier_ValidTokenWithNonce_ReturnsSubject()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", AppleIssuer, AppleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "apple-sub", nonce: "nonce-1");
        var verifier = CreateAppleVerifier(TestJwksProviderFactory.Create(jwks));

        var principal = await verifier.VerifyAsync(token, "nonce-1", CancellationToken.None);

        Assert.Equal("apple-sub", principal.SubjectId);
    }

    /// <summary>
    /// Apple：客户端传原始 nonce、令牌中为其 SHA-256 摘要（真机 SDK 行为）时通过验证。
    /// </summary>
    [Fact]
    public async Task AppleVerifier_HashedNonceInToken_ReturnsSubject()
    {
        const string rawNonce = "raw-nonce-abc";
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", AppleIssuer, AppleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "apple-sub", nonce: Sha256Hex(rawNonce));
        var verifier = CreateAppleVerifier(TestJwksProviderFactory.Create(jwks));

        var principal = await verifier.VerifyAsync(token, rawNonce, CancellationToken.None);

        Assert.Equal("apple-sub", principal.SubjectId);
    }

    /// <summary>
    /// Apple：email_verified 为字符串 "true" 时把邮箱交给上层用于匹配账户。
    /// </summary>
    [Fact]
    public async Task AppleVerifier_VerifiedEmail_ReturnsVerifiedEmail()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", AppleIssuer, AppleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "apple-sub",
            nonce: "nonce-1", email: "u@example.com", emailVerified: "true");
        var verifier = CreateAppleVerifier(TestJwksProviderFactory.Create(jwks));

        var principal = await verifier.VerifyAsync(token, "nonce-1", CancellationToken.None);

        Assert.Equal("u@example.com", principal.VerifiedEmail);
    }

    /// <summary>
    /// Apple：邮箱未验证时不向上层透出邮箱，避免用不可信邮箱匹配或创建账户。
    /// </summary>
    [Fact]
    public async Task AppleVerifier_UnverifiedEmail_DoesNotReturnEmail()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", AppleIssuer, AppleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "apple-sub",
            nonce: "nonce-1", email: "u@example.com", emailVerified: "false");
        var verifier = CreateAppleVerifier(TestJwksProviderFactory.Create(jwks));

        var principal = await verifier.VerifyAsync(token, "nonce-1", CancellationToken.None);

        Assert.Null(principal.VerifiedEmail);
    }

    /// <summary>
    /// Google：email_verified 为布尔 true 时把邮箱交给上层。
    /// </summary>
    [Fact]
    public async Task GoogleVerifier_VerifiedEmail_ReturnsVerifiedEmail()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", GoogleIssuer, GoogleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "google-sub",
            email: "u@example.com", emailVerified: true);
        var verifier = CreateGoogleVerifier(TestJwksProviderFactory.Create(jwks));

        var principal = await verifier.VerifyAsync(token, CancellationToken.None);

        Assert.Equal("u@example.com", principal.VerifiedEmail);
    }

    /// <summary>
    /// Apple：要求 nonce 但请求未传时拒绝。
    /// </summary>
    [Fact]
    public async Task AppleVerifier_MissingNonce_Throws()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", AppleIssuer, AppleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "apple-sub", nonce: "nonce-1");
        var verifier = CreateAppleVerifier(TestJwksProviderFactory.Create(jwks));

        await Assert.ThrowsAsync<KnownException>(() => verifier.VerifyAsync(token, nonce: null, CancellationToken.None));
    }

    /// <summary>
    /// Apple：缓存中 JWKS 缺少 kid 时会强制刷新一次并成功（模拟 Apple 轮换密钥）。
    /// </summary>
    [Fact]
    public async Task AppleVerifier_JwksKeyRotated_RefreshesAndSucceeds()
    {
        using var oldKey = TestIdTokenFactory.CreateRsaKey("old-kid", out var oldJwks);
        using var newKey = TestIdTokenFactory.CreateRsaKey("new-kid", out var newJwks);
        var token = TestIdTokenFactory.CreateToken(
            newKey, "new-kid", "RS256", AppleIssuer, AppleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "apple-sub", nonce: "nonce-1");
        var client = new StubAppleAuthClient(oldJwks, newJwks);
        var verifier = CreateAppleVerifier(TestJwksProviderFactory.Create(client, new StubGoogleAuthClient(newJwks)));

        var principal = await verifier.VerifyAsync(token, "nonce-1", CancellationToken.None);

        Assert.Equal("apple-sub", principal.SubjectId);
        Assert.Equal(2, client.CallCount);
    }

    /// <summary>
    /// Google：合法 RS256 令牌通过验证。
    /// </summary>
    [Fact]
    public async Task GoogleVerifier_ValidRs256Token_ReturnsSubject()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", GoogleIssuer, GoogleAudience,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "google-sub");
        var verifier = CreateGoogleVerifier(TestJwksProviderFactory.Create(jwks));

        var principal = await verifier.VerifyAsync(token, CancellationToken.None);

        Assert.Equal("google-sub", principal.SubjectId);
    }

    /// <summary>
    /// Google：受众不在白名单时拒绝。
    /// </summary>
    [Fact]
    public async Task GoogleVerifier_WrongAudience_Throws()
    {
        using var key = TestIdTokenFactory.CreateRsaKey(out var jwks);
        var token = TestIdTokenFactory.CreateToken(
            key, TestIdTokenFactory.RsaKeyId, "RS256", GoogleIssuer, "other-client",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "google-sub");
        var verifier = CreateGoogleVerifier(TestJwksProviderFactory.Create(jwks));

        await Assert.ThrowsAsync<KnownException>(() => verifier.VerifyAsync(token, CancellationToken.None));
    }

    /// <summary>
    /// 计算字符串的 SHA-256 十六进制摘要（与 Apple SDK 写入令牌的形式一致）。
    /// </summary>
    private static string Sha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// 构建 Apple 验证器。
    /// </summary>
    private static AppleExternalIdentityVerifier CreateAppleVerifier(PlatformJwksProvider jwksProvider)
    {
        var options = new AppleExternalIdentityOptions
        {
            AllowedAudiences = new[] { AppleAudience },
            RequireNonce = true
        };
        return new AppleExternalIdentityVerifier(options: Microsoft.Extensions.Options.Options.Create(options), jwksProvider: jwksProvider, logger: NullLogger<AppleExternalIdentityVerifier>.Instance);
    }

    /// <summary>
    /// 构建 Google 验证器。
    /// </summary>
    private static GoogleExternalIdentityVerifier CreateGoogleVerifier(PlatformJwksProvider jwksProvider)
    {
        var options = new GoogleExternalIdentityOptions
        {
            AllowedAudiences = new[] { GoogleAudience }
        };
        return new GoogleExternalIdentityVerifier(options: Microsoft.Extensions.Options.Options.Create(options), jwksProvider: jwksProvider, logger: NullLogger<GoogleExternalIdentityVerifier>.Instance);
    }
}