using Bumpic.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreNotification;

/// <summary>
/// 持久化已验证来源的商店通知命令。
/// </summary>
public record ReceiveStoreNotificationCommand(
    AppStore Store,
    string ExternalNotificationId,
    string NotificationType,
    int SchemaVersion,
    int ParserVersion,
    string RawPayload,
    string? NormalizedPayload,
    DateTimeOffset SourceVerifiedAt,
    string SourcePrincipal,
    string? SourceAudience,
    DateTimeOffset OccurredAt) : ICommand<ReceiveStoreNotificationResult>;

/// <summary>
/// 持久化商店通知命令处理器。
/// </summary>
public class ReceiveStoreNotificationCommandHandler(
    IStoreNotificationReceiptRepository repository,
    IClock clock) : ICommandHandler<ReceiveStoreNotificationCommand, ReceiveStoreNotificationResult>
{
    /// <summary>
    /// 保存可重放的明文通知；重复平台通知只返回已有收件记录。
    /// </summary>
    public async Task<ReceiveStoreNotificationResult> Handle(
        ReceiveStoreNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(
            store: request.Store,
            externalNotificationId: request.ExternalNotificationId,
            cancellationToken: cancellationToken);
        if (existing is not null)
        {
            return new ReceiveStoreNotificationResult(
                StoreNotificationReceiptId: existing.Id,
                Created: false);
        }

        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RawPayload));
        var receipt = new StoreNotificationReceipt(
            store: request.Store,
            externalNotificationId: request.ExternalNotificationId,
            notificationType: request.NotificationType,
            schemaVersion: request.SchemaVersion,
            parserVersion: request.ParserVersion,
            rawPayload: request.RawPayload,
            normalizedPayload: string.IsNullOrWhiteSpace(request.NormalizedPayload)
                ? null
                : request.NormalizedPayload,
            payloadHash: payloadHash,
            sourceVerifiedAt: request.SourceVerifiedAt,
            sourcePrincipal: request.SourcePrincipal,
            sourceAudience: request.SourceAudience,
            occurredAt: request.OccurredAt,
            now: clock.UtcNow);
        await repository.AddAsync(receipt, cancellationToken);
        return new ReceiveStoreNotificationResult(
            StoreNotificationReceiptId: receipt.Id,
            Created: true);
    }
}

/// <summary>
/// 商店通知收件命令锁。
/// </summary>
public class ReceiveStoreNotificationCommandLock : ICommandLock<ReceiveStoreNotificationCommand>
{
    /// <summary>
    /// 同一商店平台通知身份的持久化串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReceiveStoreNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var identity = $"{command.Store}:{command.ExternalNotificationId}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-notification:{digest}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 持久化商店通知命令验证器。
/// </summary>
public class ReceiveStoreNotificationCommandValidator : AbstractValidator<ReceiveStoreNotificationCommand>
{
    /// <summary>
    /// 初始化通知收件验证规则。
    /// </summary>
    public ReceiveStoreNotificationCommandValidator()
    {
        RuleFor(x => x.Store).IsInEnum();
        RuleFor(x => x.ExternalNotificationId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.NotificationType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SchemaVersion).GreaterThan(0);
        RuleFor(x => x.ParserVersion).GreaterThan(0);
        RuleFor(x => x.RawPayload).NotEmpty();
        RuleFor(x => x.SourcePrincipal).NotEmpty().MaximumLength(255);
        RuleFor(x => x.SourceAudience).MaximumLength(500);
    }
}
