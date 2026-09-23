using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// 外部身份令牌校验错误码。
/// </summary>
public static class ExternalIdTokenErrorCodes
{
    /// <summary>
    /// 对外统一错误码：凭据无效。
    /// </summary>
    public const string Invalid = "EXTERNAL_CREDENTIAL_INVALID";

    /// <summary>
    /// 内部错误码：JWKS 中未找到匹配的 kid，用于触发密钥刷新重试（不对外暴露）。
    /// </summary>
    public const string KeyNotFound = "EXTERNAL_JWKS_KEY_NOT_FOUND";

    /// <summary>内部错误码：令牌格式非法或无法解析（不对外暴露，仅用于日志定位）。</summary>
    public const string Malformed = "EXTERNAL_TOKEN_MALFORMED";

    /// <summary>内部错误码：签名算法不在白名单（不对外暴露，仅用于日志定位）。</summary>
    public const string AlgorithmNotAllowed = "EXTERNAL_TOKEN_ALGORITHM_NOT_ALLOWED";

    /// <summary>内部错误码：签名校验未通过（不对外暴露，仅用于日志定位）。</summary>
    public const string SignatureInvalid = "EXTERNAL_TOKEN_SIGNATURE_INVALID";

    /// <summary>内部错误码：签发方不在白名单（不对外暴露，仅用于日志定位）。</summary>
    public const string IssuerMismatch = "EXTERNAL_TOKEN_ISSUER_MISMATCH";

    /// <summary>内部错误码：受众不在白名单（不对外暴露，日志中会带上令牌实际的 aud，便于与配置比对）。</summary>
    public const string AudienceMismatch = "EXTERNAL_TOKEN_AUDIENCE_MISMATCH";

    /// <summary>内部错误码：令牌已过期或缺少 exp（不对外暴露，仅用于日志定位）。</summary>
    public const string Expired = "EXTERNAL_TOKEN_EXPIRED";

    /// <summary>内部错误码：令牌缺少 sub（不对外暴露，仅用于日志定位）。</summary>
    public const string SubjectMissing = "EXTERNAL_TOKEN_SUBJECT_MISSING";

    /// <summary>内部错误码：nonce 不匹配（不对外暴露，仅用于日志定位）。</summary>
    public const string NonceMismatch = "EXTERNAL_TOKEN_NONCE_MISMATCH";
}

/// <summary>
/// ID Token 校验要求。
/// </summary>
/// <param name="AllowedIssuers">允许的签发方。</param>
/// <param name="AllowedAudiences">允许的受众。</param>
/// <param name="AllowedAlgorithms">允许的签名算法（Apple / Google 实测均为 RS256，同时兼容 ES256）。</param>
/// <param name="ExpectedNonce">期望的 nonce；为 null 时不校验。</param>
/// <param name="AllowHashedNonce">
/// 是否允许令牌中的 nonce 为 ExpectedNonce 的 SHA-256 十六进制摘要。
/// Apple 要求客户端把原始 nonce 的 SHA-256 摘要交给 SDK，令牌里存的是摘要，因此需要开启；
/// Google 令牌里存的是客户端传入的原文，保持关闭以严格比对。
/// </param>
public record IdTokenValidationRequirements(
    IReadOnlyList<string> AllowedIssuers,
    IReadOnlyList<string> AllowedAudiences,
    IReadOnlyList<string> AllowedAlgorithms,
    string? ExpectedNonce,
    bool AllowHashedNonce = false);

/// <summary>
/// JWS ID Token 校验结果。
/// </summary>
/// <param name="Subject">平台用户标识（sub）。</param>
/// <param name="Email">令牌声明的邮箱；可能为空。</param>
/// <param name="EmailVerified">令牌是否声明该邮箱已验证（email_verified）；只有为 true 时邮箱才可被信任。</param>
/// <param name="Nonce">令牌中的 nonce 声明。</param>
public record IdTokenClaims(string Subject, string? Email, bool EmailVerified, string? Nonce);

/// <summary>
/// 基于 JWKS 的 ID Token 校验器：验签（ES256/RS256）后校验 iss、aud、exp、sub 与可选 nonce。
/// </summary>
public static class ExternalIdTokenValidator
{
    /// <summary>
    /// 允许的时钟偏差（秒），用于 exp 比较。
    /// </summary>
    private const int ClockSkewSeconds = 60;

    /// <summary>
    /// 校验 ID Token。
    /// </summary>
    /// <param name="token">JWS 形式的 ID Token。</param>
    /// <param name="jwksJson">JWKS JSON 文本。</param>
    /// <param name="requirements">校验要求。</param>
    /// <returns>解析出的声明。</returns>
    /// <exception cref="KnownException">校验失败；密钥未命中时为内部错误码 <see cref="ExternalIdTokenErrorCodes.KeyNotFound"/>。</exception>
    public static IdTokenClaims Validate(string token, string jwksJson, IdTokenValidationRequirements requirements)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(jwksJson))
        {
            throw new KnownException(ExternalIdTokenErrorCodes.Malformed);
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new KnownException(ExternalIdTokenErrorCodes.Malformed);
        }

        try
        {
            using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            var headerRoot = header.RootElement;
            var algorithm = GetString(headerRoot, "alg");
            var keyId = GetString(headerRoot, "kid");

            if (algorithm is null || !requirements.AllowedAlgorithms.Contains(algorithm))
            {
                throw new KnownException($"{ExternalIdTokenErrorCodes.AlgorithmNotAllowed}: {algorithm ?? "(缺失)"}");
            }

            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new KnownException(ExternalIdTokenErrorCodes.KeyNotFound);
            }

            // 先验签，再信任载荷。
            var signatureResult = VerifySignature(parts[0], parts[1], parts[2], algorithm, keyId, jwksJson);
            if (signatureResult == SignatureVerificationResult.KeyNotFound)
            {
                throw new KnownException(ExternalIdTokenErrorCodes.KeyNotFound);
            }

            if (signatureResult != SignatureVerificationResult.Valid)
            {
                throw new KnownException(ExternalIdTokenErrorCodes.SignatureInvalid);
            }

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var claims = payload.RootElement;

            var issuer = GetString(claims, "iss");
            if (issuer is null || !requirements.AllowedIssuers.Contains(issuer))
            {
                throw new KnownException($"{ExternalIdTokenErrorCodes.IssuerMismatch}: {issuer ?? "(缺失)"}");
            }

            if (!HasAllowedAudience(claims, requirements.AllowedAudiences))
            {
                throw new KnownException($"{ExternalIdTokenErrorCodes.AudienceMismatch}: {DescribeAudiences(claims)}");
            }

            if (!claims.TryGetProperty("exp", out var expElement) || !expElement.TryGetInt64(out var expiresAt))
            {
                throw new KnownException($"{ExternalIdTokenErrorCodes.Expired}: exp 缺失");
            }

            var expiresAtTime = DateTimeOffset.FromUnixTimeSeconds(expiresAt).AddSeconds(ClockSkewSeconds);
            if (DateTimeOffset.UtcNow >= expiresAtTime)
            {
                throw new KnownException(
                    $"{ExternalIdTokenErrorCodes.Expired}: exp={DateTimeOffset.FromUnixTimeSeconds(expiresAt):u}");
            }

            var subject = GetString(claims, "sub");
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new KnownException(ExternalIdTokenErrorCodes.SubjectMissing);
            }

            var nonce = GetString(claims, "nonce");
            if (requirements.ExpectedNonce is not null && !IsExpectedNonce(nonce, requirements))
            {
                throw new KnownException($"{ExternalIdTokenErrorCodes.NonceMismatch}: 令牌 nonce={nonce ?? "(缺失)"}");
            }

            return new IdTokenClaims(subject, GetString(claims, "email"), IsEmailVerified(claims), nonce);
        }
        catch (KnownException)
        {
            throw;
        }
        catch (Exception)
        {
            // 畸形 token（非法 Base64Url、非法 JSON 等）统一转为错误码，避免 500；调用方会转成对外统一错误码。
            throw new KnownException(ExternalIdTokenErrorCodes.Malformed);
        }
    }

    /// <summary>
    /// 判断令牌中的 nonce 是否与期望值匹配。
    /// </summary>
    /// <param name="nonce">令牌中的 nonce 声明。</param>
    /// <param name="requirements">校验要求。</param>
    /// <returns>匹配返回 true。</returns>
    private static bool IsExpectedNonce(string? nonce, IdTokenValidationRequirements requirements)
    {
        if (nonce is null)
        {
            return false;
        }

        if (string.Equals(nonce, requirements.ExpectedNonce, StringComparison.Ordinal))
        {
            return true;
        }

        // Apple：客户端交给 SDK 的是原始 nonce 的 SHA-256 摘要，摘要以十六进制写在令牌里。
        return requirements.AllowHashedNonce &&
            string.Equals(nonce, Sha256Hex(requirements.ExpectedNonce!), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 计算字符串的 SHA-256 十六进制摘要（小写）。
    /// </summary>
    /// <param name="value">待计算的字符串。</param>
    /// <returns>小写十六进制摘要。</returns>
    private static string Sha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    /// <summary>
    /// 读取令牌中的 aud（字符串或数组），用于失败日志中与配置白名单比对。
    /// </summary>
    /// <param name="claims">令牌负载。</param>
    /// <returns>逗号分隔的受众；缺失时返回 "(缺失)"。</returns>
    private static string DescribeAudiences(JsonElement claims)
    {
        if (!claims.TryGetProperty("aud", out var audience))
        {
            return "(缺失)";
        }

        return audience.ValueKind switch
        {
            JsonValueKind.String => audience.GetString() ?? "(缺失)",
            JsonValueKind.Array => string.Join(",", audience.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())),
            _ => "(无法识别)"
        };
    }

    /// <summary>
    /// 读取 email_verified 声明：Google 使用布尔值，Apple 使用字符串 "true"/"false"。
    /// </summary>
    /// <param name="element">令牌负载。</param>
    /// <returns>邮箱已验证返回 true；声明缺失或无法识别返回 false。</returns>
    private static bool IsEmailVerified(JsonElement element)
    {
        if (!element.TryGetProperty("email_verified", out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(property.GetString(), out var verified) && verified,
            _ => false
        };
    }

    /// <summary>
    /// 读取字符串声明。
    /// </summary>
    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    /// <summary>
    /// 校验 aud 是否命中允许列表（支持字符串或数组形式）。
    /// </summary>
    private static bool HasAllowedAudience(JsonElement claims, IReadOnlyList<string> allowedAudiences)
    {
        if (!claims.TryGetProperty("aud", out var audienceElement))
        {
            return false;
        }

        if (audienceElement.ValueKind == JsonValueKind.String)
        {
            return allowedAudiences.Contains(audienceElement.GetString() ?? string.Empty);
        }

        if (audienceElement.ValueKind == JsonValueKind.Array)
        {
            return audienceElement.EnumerateArray()
                .Any(item => item.ValueKind == JsonValueKind.String &&
                             allowedAudiences.Contains(item.GetString() ?? string.Empty));
        }

        return false;
    }

    /// <summary>
    /// 验签结果。
    /// </summary>
    private enum SignatureVerificationResult
    {
        /// <summary>签名有效。</summary>
        Valid,

        /// <summary>签名无效或 JWKS 不可用。</summary>
        Invalid,

        /// <summary>JWKS 中没有匹配 kid 的可用密钥（可由上层刷新密钥后重试）。</summary>
        KeyNotFound
    }

    /// <summary>
    /// 在 JWKS 中查找与 kid、算法匹配且可用于签名的密钥；调用方需保证 JsonDocument 生命周期。
    /// </summary>
    private static JsonElement? FindMatchingKey(JsonElement keysElement, string keyId, string algorithm)
    {
        var expectedKeyType = algorithm == "ES256" ? "EC" : "RSA";
        foreach (var key in keysElement.EnumerateArray())
        {
            if (GetString(key, "kid") != keyId)
            {
                continue;
            }

            if (GetString(key, "kty") != expectedKeyType)
            {
                continue;
            }

            var use = GetString(key, "use");
            if (use is not null && use != "sig")
            {
                continue;
            }

            var keyAlgorithm = GetString(key, "alg");
            if (keyAlgorithm is not null && keyAlgorithm != algorithm)
            {
                continue;
            }

            return key;
        }

        return null;
    }

    /// <summary>
    /// 在 JWKS 中定位密钥并验签：ES256 用 P-256 公钥，RS256 用 RSA 公钥。
    /// </summary>
    private static SignatureVerificationResult VerifySignature(
        string headerPart,
        string payloadPart,
        string signaturePart,
        string algorithm,
        string keyId,
        string jwksJson)
    {
        try
        {
            using var jwks = JsonDocument.Parse(jwksJson);
            if (!jwks.RootElement.TryGetProperty("keys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array)
            {
                return SignatureVerificationResult.Invalid;
            }

            var key = FindMatchingKey(keysElement, keyId, algorithm);
            if (key is null)
            {
                return SignatureVerificationResult.KeyNotFound;
            }

            var signedData = Encoding.ASCII.GetBytes($"{headerPart}.{payloadPart}");
            var signatureBytes = Base64UrlDecodeBytes(signaturePart);
            var verified = algorithm == "ES256"
                ? VerifyEs256(signedData, signatureBytes, key.Value)
                : VerifyRs256(signedData, signatureBytes, key.Value);
            return verified ? SignatureVerificationResult.Valid : SignatureVerificationResult.Invalid;
        }
        catch (Exception)
        {
            return SignatureVerificationResult.Invalid;
        }
    }

    /// <summary>
    /// ES256（ECDSA P-256 + SHA-256，IEEE P1363 签名格式）。
    /// </summary>
    private static bool VerifyEs256(byte[] signedData, byte[] signatureBytes, JsonElement key)
    {
        if (GetString(key, "crv") != "P-256")
        {
            return false;
        }

        var x = GetString(key, "x");
        var y = GetString(key, "y");
        if (x is null || y is null)
        {
            return false;
        }

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64UrlDecodeBytes(x), Y = Base64UrlDecodeBytes(y) }
        });
        return ecdsa.VerifyData(signedData, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// RS256（RSA PKCS#1 v1.5 + SHA-256）。
    /// </summary>
    private static bool VerifyRs256(byte[] signedData, byte[] signatureBytes, JsonElement key)
    {
        var modulus = GetString(key, "n");
        var exponent = GetString(key, "e");
        if (modulus is null || exponent is null)
        {
            return false;
        }

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlDecodeBytes(modulus),
            Exponent = Base64UrlDecodeBytes(exponent)
        });
        return rsa.VerifyData(signedData, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Base64Url 解码为 UTF-8 文本。
    /// </summary>
    private static string Base64UrlDecode(string input)
    {
        return Encoding.UTF8.GetString(Base64UrlDecodeBytes(input));
    }

    /// <summary>
    /// Base64Url 解码为字节数组。
    /// </summary>
    internal static byte[] Base64UrlDecodeBytes(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}