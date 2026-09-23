using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Services.Store;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 记录 Google Play 待消费交易的一次失败并安排下次补偿。
/// </summary>
public record RecordGoogleStoreConsumptionFailureCommand(
    StoreTransactionId StoreTransactionId,
    string FailureCode,
    bool IsRetryable) : ICommand;

/// <summary>
/// Google Play 待消费失败记录命令处理器。
/// </summary>
public class RecordGoogleStoreConsumptionFailureCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<RecordGoogleStoreConsumptionFailureCommand>
{
    /// <inheritdoc />
    public async Task Handle(
        RecordGoogleStoreConsumptionFailureCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(request.StoreTransactionId, cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        if (transaction.Status != StoreTransactionStatus.PendingConsumption)
        {
            return;
        }

        var now = clock.UtcNow;
        transaction.RecordConsumptionFailure(
            failureCode: request.FailureCode,
            nextRetryAt: now.Add(StoreConsumptionRetryPolicy.CalculateDelay(
                completedAttemptCount: transaction.ConsumptionAttemptCount,
                isRetryable: request.IsRetryable)),
            now: now);
    }
}

/// <summary>
/// Google Play 待消费失败记录命令锁。
/// </summary>
public class RecordGoogleStoreConsumptionFailureCommandLock
    : ICommandLock<RecordGoogleStoreConsumptionFailureCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        RecordGoogleStoreConsumptionFailureCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-transaction:{command.StoreTransactionId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// Google Play 待消费失败记录命令验证器。
/// </summary>
public class RecordGoogleStoreConsumptionFailureCommandValidator
    : AbstractValidator<RecordGoogleStoreConsumptionFailureCommand>
{
    public RecordGoogleStoreConsumptionFailureCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId.Id).NotEmpty();
        RuleFor(x => x.FailureCode).NotEmpty().MaximumLength(100);
    }
}
