using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetCorePal.Extensions.Primitives;
using Bumpic.Domain.Enums;
using Bumpic.Web.Services.EmailCodes;

namespace Bumpic.Web.Tests.Services.EmailCodes;

/// <summary>
/// 邮箱验证码业务门面测试：重点覆盖跨用途共用冷却，避免在 Register 与 Login 之间切换绕过重发限制。
/// </summary>
public class EmailCodeServiceTests
{
    /// <summary>
    /// 首次发送成功：写入共用冷却与用途冷却，并按配置返回重发间隔。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_FirstSend_MarksBothCooldowns()
    {
        var store = new FakeEmailCodeStore();
        var sender = new FakeEmailCodeSender();
        var service = CreateService(store, sender);

        var resendAfter = await service.SendCodeAsync(
            "user@example.com",
            OperationType.Register,
            ipAddress: "1.2.3.4",
            deviceId: "device-1",
            CancellationToken.None);

        Assert.Equal(60, resendAfter);
        Assert.True(store.SharedCooldownMarked);
        Assert.Contains(OperationType.Register, store.MarkedCooldowns);
        Assert.Single(sender.Sent);
    }

    /// <summary>
    /// 发布审核邮箱固定验证码开启且邮箱匹配时，保存并发送配置的验证码。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_PublishEmailMatches_SendsConfiguredCode()
    {
        var store = new FakeEmailCodeStore();
        var sender = new FakeEmailCodeSender();
        var service = CreateService(
            store,
            sender,
            new ForPublishEmailOption
            {
                IsOpen = true,
                PublishEmailAddress = "publish@example.com",
                PublishEmailCode = "135790"
            });

        await service.SendCodeAsync(
            "publish@example.com",
            OperationType.Register,
            ipAddress: null,
            deviceId: null,
            CancellationToken.None);

        Assert.Equal("135790", Assert.Single(sender.Sent).Code);
        Assert.Equal(RedisEmailCodeStore.HashCode("135790"), store.SavedCodeDigest);
    }

    /// <summary>
    /// 发布审核邮箱固定验证码关闭时，即使邮箱匹配也继续生成普通数字验证码。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_PublishEmailOptionClosed_GeneratesCode()
    {
        var store = new FakeEmailCodeStore();
        var sender = new FakeEmailCodeSender();
        var service = CreateService(
            store,
            sender,
            new ForPublishEmailOption
            {
                IsOpen = false,
                PublishEmailAddress = "publish@example.com",
                PublishEmailCode = "fixed-code"
            });

        await service.SendCodeAsync(
            "publish@example.com",
            OperationType.Register,
            ipAddress: null,
            deviceId: null,
            CancellationToken.None);

        Assert.NotEqual("fixed-code", Assert.Single(sender.Sent).Code);
    }

    /// <summary>
    /// 已处于共用冷却中时，换一个 purpose 再发仍然被拒绝。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_SharedCooldownActive_RejectsOtherPurpose()
    {
        var store = new FakeEmailCodeStore { SharedCooldownActive = true };
        var sender = new FakeEmailCodeSender();
        var service = CreateService(store, sender);

        var exception = await Assert.ThrowsAsync<KnownException>(() => service.SendCodeAsync(
            "user@example.com",
            OperationType.Login,
            ipAddress: null,
            deviceId: null,
            CancellationToken.None));

        Assert.Equal("EMAIL_RATE_LIMITED", exception.Message);
        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// 用途自身处于冷却中时同样拒绝，不触发邮件发送。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_PurposeCooldownActive_RejectsSend()
    {
        var store = new FakeEmailCodeStore();
        store.CooldownPurposes.Add(OperationType.Register);
        var sender = new FakeEmailCodeSender();
        var service = CreateService(store, sender);

        await Assert.ThrowsAsync<KnownException>(() => service.SendCodeAsync(
            "user@example.com",
            OperationType.Register,
            ipAddress: null,
            deviceId: null,
            CancellationToken.None));

        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// 超出限流窗口上限时不写冷却键：用户换网络后可立即重试，无需再等冷却。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_RateLimited_DoesNotMarkCooldown()
    {
        var store = new FakeEmailCodeStore { AllowSend = false };
        var sender = new FakeEmailCodeSender();
        var service = CreateService(store, sender);

        var exception = await Assert.ThrowsAsync<KnownException>(() => service.SendCodeAsync(
            "user@example.com",
            OperationType.Register,
            ipAddress: null,
            deviceId: null,
            CancellationToken.None));

        Assert.Equal("EMAIL_RATE_LIMITED", exception.Message);
        Assert.False(store.SharedCooldownMarked);
        Assert.Empty(store.MarkedCooldowns);
        Assert.Empty(sender.Sent);
    }

    /// <summary>
    /// 邮件发送失败时清除已保存的验证码，并抛出可对外的错误码。
    /// </summary>
    [Fact]
    public async Task SendCodeAsync_SenderFails_RemovesCodeAndThrows()
    {
        var store = new FakeEmailCodeStore();
        var sender = new FakeEmailCodeSender { Failure = new InvalidOperationException("smtp down") };
        var service = CreateService(store, sender);

        var exception = await Assert.ThrowsAsync<KnownException>(() => service.SendCodeAsync(
            "user@example.com",
            OperationType.Register,
            ipAddress: null,
            deviceId: null,
            CancellationToken.None));

        Assert.Equal("EMAIL_SEND_FAILED", exception.Message);
        Assert.Contains((OperationType.Register), store.RemovedCodes);
    }

    /// <summary>
    /// 核销失败时抛出 INVALID_EMAIL_CODE。
    /// </summary>
    [Fact]
    public async Task VerifyAndConsumeAsync_CodeMismatch_Throws()
    {
        var store = new FakeEmailCodeStore { ConsumeSucceeds = false };
        var service = CreateService(store, new FakeEmailCodeSender());

        var exception = await Assert.ThrowsAsync<KnownException>(() => service.VerifyAndConsumeAsync(
            "user@example.com",
            OperationType.Login,
            "000000",
            CancellationToken.None));

        Assert.Equal("INVALID_EMAIL_CODE", exception.Message);
    }

    /// <summary>
    /// 核销成功时不抛异常。
    /// </summary>
    [Fact]
    public async Task VerifyAndConsumeAsync_CodeMatches_Succeeds()
    {
        var store = new FakeEmailCodeStore { ConsumeSucceeds = true };
        var service = CreateService(store, new FakeEmailCodeSender());

        await service.VerifyAndConsumeAsync(
            "user@example.com",
            OperationType.Login,
            "123456",
            CancellationToken.None);

        Assert.Equal(1, store.ConsumeAttempts);
    }

    private static EmailCodeService CreateService(
        FakeEmailCodeStore store,
        FakeEmailCodeSender sender,
        ForPublishEmailOption? forPublishEmailOption = null)
    {
        return new EmailCodeService(
            store,
            sender,
            Microsoft.Extensions.Options.Options.Create(new EmailCodeOptions()),
            Microsoft.Extensions.Options.Options.Create(forPublishEmailOption ?? new ForPublishEmailOption()),
            NullLogger<EmailCodeService>.Instance);
    }

    /// <summary>
    /// 内存版验证码存储：只记录被调用的动作，不做真实过期与计数。
    /// </summary>
    private sealed class FakeEmailCodeStore : IEmailCodeStore
    {
        public bool SharedCooldownActive { get; set; }

        public bool SharedCooldownMarked { get; private set; }

        public bool AllowSend { get; set; } = true;

        public bool ConsumeSucceeds { get; set; }

        public int ConsumeAttempts { get; private set; }

        public string SavedCodeDigest { get; private set; } = string.Empty;

        public HashSet<OperationType> CooldownPurposes { get; } = [];

        public List<OperationType> MarkedCooldowns { get; } = [];

        public List<OperationType> RemovedCodes { get; } = [];

        public Task<bool> IsInCooldownAsync(string email, OperationType purpose, CancellationToken cancellationToken)
        {
            return Task.FromResult(CooldownPurposes.Contains(purpose));
        }

        public Task<bool> MarkCooldownAsync(string email, OperationType purpose, TimeSpan cooldown, CancellationToken cancellationToken)
        {
            MarkedCooldowns.Add(purpose);
            CooldownPurposes.Add(purpose);
            return Task.FromResult(true);
        }

        public Task<bool> IsInSharedCooldownAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(SharedCooldownActive);
        }

        public Task<bool> MarkSharedCooldownAsync(string email, TimeSpan cooldown, CancellationToken cancellationToken)
        {
            SharedCooldownMarked = true;
            return Task.FromResult(true);
        }

        public Task<bool> TryRegisterSendAsync(string email, OperationType purpose, string? ipAddress, string? deviceId, TimeSpan window, int maxRequests, CancellationToken cancellationToken)
        {
            return Task.FromResult(AllowSend);
        }

        public Task SaveCodeAsync(string email, OperationType purpose, string codeDigest, TimeSpan ttl, CancellationToken cancellationToken)
        {
            SavedCodeDigest = codeDigest;
            return Task.CompletedTask;
        }

        public Task<bool> TryConsumeCodeAsync(string email, OperationType purpose, string codeDigest, int maxAttempts, CancellationToken cancellationToken)
        {
            ConsumeAttempts++;
            return Task.FromResult(ConsumeSucceeds);
        }

        public Task RemoveCodeAsync(string email, OperationType purpose, CancellationToken cancellationToken)
        {
            RemovedCodes.Add(purpose);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录发送动作的邮件发送替身。
    /// </summary>
    private sealed class FakeEmailCodeSender : IEmailCodeSender
    {
        public Exception? Failure { get; set; }

        public List<(string ToAddress, OperationType Purpose, string Code)> Sent { get; } = [];

        public Task SendAsync(string toAddress, OperationType purpose, string code, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Sent.Add((toAddress, purpose, code));
            return Task.CompletedTask;
        }
    }
}
