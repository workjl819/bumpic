using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreNotification;

/// <summary>
/// 记录商店通知处理失败命令。
/// </summary>
public record FailStoreNotificationCommand(
    StoreNotificationReceiptId StoreNotificationReceiptId,
    string LeaseOwner,
    string FailureCode,
    DateTimeOffset? NextRetryAt,
    bool DeadLetter) : ICommand;

/// <summary>
/// 记录商店通知处理失败命令处理器。
/// </summary>
public class FailStoreNotificationCommandHandler(
    IStoreNotificationReceiptRepository repository,
    IClock clock) : ICommandHandler<FailStoreNotificationCommand>
{
    /// <inheritdoc />
    public async Task Handle(FailStoreNotificationCommand request, CancellationToken cancellationToken)
    {
        var receipt = await repository.GetAsync(request.StoreNotificationReceiptId, cancellationToken)
                      ?? throw new KnownException("STORE_NOTIFICATION_NOT_FOUND");
        receipt.Fail(
            leaseOwner: request.LeaseOwner,
            failureCode: request.FailureCode,
            nextRetryAt: request.NextRetryAt,
            deadLetter: request.DeadLetter,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 商店通知失败命令锁。
/// </summary>
public class FailStoreNotificationCommandLock : ICommandLock<FailStoreNotificationCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        FailStoreNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-notification-id:{command.StoreNotificationReceiptId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 记录商店通知处理失败命令验证器。
/// </summary>
public class FailStoreNotificationCommandValidator : AbstractValidator<FailStoreNotificationCommand>
{
    public FailStoreNotificationCommandValidator()
    {
        RuleFor(x => x.LeaseOwner).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FailureCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NextRetryAt).NotNull().When(x => !x.DeadLetter);
        RuleFor(x => x.NextRetryAt).Null().When(x => x.DeadLetter);
    }
}
