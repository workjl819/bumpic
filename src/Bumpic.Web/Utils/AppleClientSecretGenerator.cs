using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Bumpic.Web.Utils;

/// <summary>
/// 生成 Sign in with Apple 所需的客户端密钥。
/// </summary>
public static class AppleClientSecretGenerator
{
    /// <summary>
    /// 使用 Apple 的 P8 私钥生成 ES256 签名的客户端密钥。
    /// </summary>
    public static string Generate(
        string teamId,
        string keyId,
        string clientId,
        string privateKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);

        var now = DateTime.UtcNow;

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);

        var securityKey = new ECDsaSecurityKey(ecdsa)
        {
            KeyId = keyId
        };

        // 注意：Microsoft.IdentityModel 默认缓存签名提供方（CacheSignatureProviders=true），
        // 缓存项会持有本次创建的 ECDsa；下一次调用命中缓存时密钥已被 using 释放，
        // 从而在 SignHash 处抛 ObjectDisposedException（表现为「第一次能用、之后都失败」）。
        // 这里关闭该凭据的签名提供方缓存，保证每次调用都是独立且有效的密钥。
        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.EcdsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = teamId,
            Audience = "https://appleid.apple.com",
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, clientId)
            ]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = signingCredentials
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(tokenDescriptor);
    }
}