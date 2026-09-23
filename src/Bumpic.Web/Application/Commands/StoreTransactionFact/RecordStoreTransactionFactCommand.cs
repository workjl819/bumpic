using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using StoreTransactionFactEntity = Bumpic.Domain.AggregateModel.StoreTransactionFactAggregate.StoreTransactionFact;

namespace Bumpic.Web.Application.Commands.StoreTransactionFact;

/// <summary>
/// 追加商店权威交易事实命令。
/// </summary>
public record RecordStoreTransactionFactCommand(
    StoreNotificationReceiptId? StoreNotificationReceiptId,
    StoreTransactionFactSourceType SourceType,
    AppStore Store,
    string ExternalTransactionId,
    string? StoreOrderId,
    string ExternalEventId,
    StoreTransactionFactType Type,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PlatformVersionAt,
    byte[] FactKey,
    byte[] PayloadHash,
    StoreTransactionId? AppliedStoreTransactionId) : ICommand<StoreTransactionFactId>;

/// <summary>
/// 追加商店权威交易事实命令处理器。
/// </summary>
public class RecordStoreTransactionFactCommandHandler(
    IStoreTransactionFactRepository repository,
    IClock clock) : ICommandHandler<RecordStoreTransactionFactCommand, StoreTransactionFactId>
{
    /// <inheritdoc />
    public async Task<StoreTransactionFactId> Handle(
        RecordStoreTransactionFactCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindByFactKeyAsync(
            store: request.Store,
            factKey: request.FactKey,
            cancellationToken: cancellationToken);
        if (existing is not null)
        {
            if (request.AppliedStoreTransactionId is not null)
            {
                existing.MarkApplied(
                    storeTransactionId: request.AppliedStoreTransactionId,
                    now: clock.UtcNow);
            }

            return existing.Id;
        }

        var now = clock.UtcNow;
        var fact = new StoreTransactionFactEntity(
            storeNotificationReceiptId: request.StoreNotificationReceiptId,
            sourceType: request.SourceType,
            store: request.Store,
            externalTransactionId: request.ExternalTransactionId,
            storeOrderId: request.StoreOrderId,
            externalEventId: request.ExternalEventId,
            type: request.Type,
            occurredAt: request.OccurredAt,
            platformVersionAt: request.PlatformVersionAt,
            observedAt: now,
            factKey: request.FactKey,
            payloadHash: request.PayloadHash,
            now: now);
        await repository.AddAsync(fact, cancellationToken);
        if (request.AppliedStoreTransactionId is not null)
        {
            fact.MarkApplied(storeTransactionId: request.AppliedStoreTransactionId, now: now);
        }

        return fact.Id;
    }
}

/// <summary>
/// 商店交易事实追加命令锁。
/// </summary>
public class RecordStoreTransactionFactCommandLock : ICommandLock<RecordStoreTransactionFactCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        RecordStoreTransactionFactCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-fact:{command.Store}:{Convert.ToHexString(command.FactKey)}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 追加商店权威交易事实命令验证器。
/// </summary>
public class RecordStoreTransactionFactCommandValidator : AbstractValidator<RecordStoreTransactionFactCommand>
{
    public RecordStoreTransactionFactCommandValidator()
    {
        RuleFor(x => x.SourceType).IsInEnum();
        RuleFor(x => x.Store).IsInEnum();
        RuleFor(x => x.ExternalTransactionId).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.StoreOrderId).MaximumLength(255);
        RuleFor(x => x.ExternalEventId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.FactKey).Must(x => x.Length == 32);
        RuleFor(x => x.PayloadHash).Must(x => x.Length == 32);
        RuleFor(x => x.StoreNotificationReceiptId)
            .NotNull()
            .When(x => x.SourceType == StoreTransactionFactSourceType.Notification);
    }
}
