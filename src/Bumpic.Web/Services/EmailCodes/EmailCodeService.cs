using Microsoft.Extensions.Options;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 邮箱验证码业务门面：负责发送限流、冷却、摘要保存与核销编排。
/// </summary>
public class EmailCodeService(
    IEmailCodeStore store,
    IEmailCodeSender sender,
    IOptions<EmailCodeOptions> options,
    IOptions<ForPublishEmailOption> forPublishEmailOption,
    ILogger<EmailCodeService> logger) : IEmailCodeService
{
    /// <inheritdoc />
    public async Task<int> SendCodeAsync(
        string email,
        OperationType purpose,
        string? ipAddress,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        // 公共冷却先判：否则调用方可以在 Register 与 Login 之间切换 purpose 绕过 60 秒重发限制。
        if (await store.IsInCooldownAsync(email, purpose, cancellationToken)
            || await store.IsInSharedCooldownAsync(email, cancellationToken))
        {
            throw new KnownException("EMAIL_RATE_LIMITED");
        }

        var window = TimeSpan.FromMinutes(config.RateLimitWindowMinutes);
        if (!await store.TryRegisterSendAsync(
                email,
                purpose,
                ipAddress,
                deviceId,
                window,
                config.RateLimitMaxRequests,
                cancellationToken))
        {
            // 限流命中时不写入冷却键：用户换网络后无需再等冷却。
            throw new KnownException("EMAIL_RATE_LIMITED");
        }

        var cooldown = TimeSpan.FromSeconds(config.ResendCooldownSeconds);
        // 同时写公共冷却与 purpose 冷却：公共冷却拦截跨用途绕过，purpose 冷却保持各用途的独立重发节奏。
        if (!await store.MarkSharedCooldownAsync(email, cooldown, cancellationToken)
            || !await store.MarkCooldownAsync(email, purpose, cooldown, cancellationToken))
        {
            throw new KnownException("EMAIL_RATE_LIMITED");
        }

        var publishEmailConfig = forPublishEmailOption.Value;
        var code = publishEmailConfig.IsOpen && email == publishEmailConfig.PublishEmailAddress
            ? publishEmailConfig.PublishEmailCode
            : GenerateCode(config.CodeLength);
        var digest = RedisEmailCodeStore.HashCode(code);
        await store.SaveCodeAsync(
            email,
            purpose,
            digest,
            TimeSpan.FromSeconds(config.CodeTtlSeconds),
            cancellationToken);

        try
        {
            await sender.SendAsync(email, purpose, code, cancellationToken);
        }
        catch (Exception exception)
        {
            await store.RemoveCodeAsync(email, purpose, cancellationToken);
            logger.LogWarning(exception, "验证码邮件发送失败：收件人 {ToAddress}，用途 {Purpose}", email, purpose);
            throw new KnownException("EMAIL_SEND_FAILED");
        }

        return config.ResendCooldownSeconds;
    }

    /// <inheritdoc />
    public async Task VerifyAndConsumeAsync(
        string email,
        OperationType purpose,
        string code,
        CancellationToken cancellationToken)
    {
        var digest = RedisEmailCodeStore.HashCode(code);
        var consumed = await store.TryConsumeCodeAsync(
            email,
            purpose,
            digest,
            options.Value.MaxVerifyAttempts,
            cancellationToken);
        if (!consumed)
        {
            throw new KnownException("INVALID_EMAIL_CODE");
        }
    }

    /// <summary>
    /// 生成指定位数的数字验证码。
    /// </summary>
    private static string GenerateCode(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)('0' + Random.Shared.Next(10));
        }

        return new string(chars);
    }
}
