using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 基于 Redis 的邮箱验证码存储实现；验证码只保存 SHA-256 摘要，不保存明文。
/// </summary>
public class RedisEmailCodeStore(IConnectionMultiplexer connectionMultiplexer) : IEmailCodeStore
{
    private const string CodeKeyPrefix = "Bumpic_email_code";

    /// <inheritdoc />
    public Task<bool> IsInCooldownAsync(string email, OperationType purpose, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return database.KeyExistsAsync(BuildKey(email, purpose, "cooldown"));
    }

    /// <inheritdoc />
    public async Task<bool> MarkCooldownAsync(string email, OperationType purpose, TimeSpan cooldown, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return await database.StringSetAsync(BuildKey(email, purpose, "cooldown"), "1", cooldown, When.NotExists);
    }

    /// <inheritdoc />
    public Task<bool> IsInSharedCooldownAsync(string email, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return database.KeyExistsAsync(BuildSharedCooldownKey(email));
    }

    /// <inheritdoc />
    public async Task<bool> MarkSharedCooldownAsync(string email, TimeSpan cooldown, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return await database.StringSetAsync(BuildSharedCooldownKey(email), "1", cooldown, When.NotExists);
    }

    /// <inheritdoc />
    public async Task<bool> TryRegisterSendAsync(
        string email,
        OperationType purpose,
        string? ipAddress,
        string? deviceId,
        TimeSpan window,
        int maxRequests,
        CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var key = BuildRateKey(email, purpose, ipAddress, deviceId);
        var count = await database.StringIncrementAsync(key);
        if (count == 1)
        {
            await database.KeyExpireAsync(key, window);
        }

        return count <= maxRequests;
    }

    /// <inheritdoc />
    public Task SaveCodeAsync(string email, OperationType purpose, string codeDigest, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return database.StringSetAsync(BuildKey(email, purpose, "code"), codeDigest, ttl);
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeCodeAsync(string email, OperationType purpose, string codeDigest, int maxAttempts, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var codeKey = BuildKey(email, purpose, "code");
        var attemptsKey = BuildKey(email, purpose, "attempts");
        var stored = await database.StringGetAsync(codeKey);
        if (stored.IsNullOrEmpty)
        {
            return false;
        }

        var attempts = await database.StringIncrementAsync(attemptsKey);
        if (attempts == 1)
        {
            var ttl = await database.KeyTimeToLiveAsync(codeKey);
            if (ttl is not null)
            {
                await database.KeyExpireAsync(attemptsKey, ttl.Value);
            }
        }

        if (stored == codeDigest)
        {
            await database.KeyDeleteAsync(new RedisKey[] { codeKey, attemptsKey });
            return true;
        }

        if (attempts >= maxAttempts)
        {
            await database.KeyDeleteAsync(new RedisKey[] { codeKey, attemptsKey });
        }

        return false;
    }

    /// <inheritdoc />
    public async Task RemoveCodeAsync(string email, OperationType purpose, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(new RedisKey[]
        {
            BuildKey(email, purpose, "code"),
            BuildKey(email, purpose, "attempts"),
            BuildKey(email, purpose, "cooldown")
        });
    }

    /// <summary>
    /// 计算验证码摘要。
    /// </summary>
    public static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// 邮箱维度的公共冷却键：不区分 purpose，Register 与 Login 共用同一冷却窗口。
    /// </summary>
    private static string BuildSharedCooldownKey(string email) => $"{CodeKeyPrefix}:shared:cooldown:{email}";

    /// <summary>
    /// 生成业务键。
    /// </summary>
    private static string BuildKey(string email, OperationType purpose, string segment)
    {
        return $"{CodeKeyPrefix}:{PurposeKey(purpose)}:{email}:{segment}";
    }

    /// <summary>
    /// 生成限流计数键。
    /// </summary>
    private static string BuildRateKey(string email, OperationType purpose, string? ipAddress, string? deviceId)
    {
        return $"{CodeKeyPrefix}:{PurposeKey(purpose)}:rate:{email}:{ipAddress ?? "ip"}:{deviceId ?? "device"}";
    }

    /// <summary>
    /// 用途键片段。
    /// </summary>
    private static string PurposeKey(OperationType purpose)
    {
        return purpose switch
        {
            OperationType.Register => "register",
            OperationType.Login => "login",
            _ => "unknown"
        };
    }
}
