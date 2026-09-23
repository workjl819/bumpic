using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreNotification;

/// <summary>
/// 完成商店通知处理命令。
/// </summary>
public record CompleteStoreNotificationCommand(
    StoreNotificationReceiptId StoreNotificationReceiptId,
    string LeaseOwner) : ICommand;

/// <summary>
/// 完成商店通知处理命令处理器。
/// </summary>
public class CompleteStoreNotificationCommandHandler(
    IStoreNotificationReceiptRepository repository,
    IClock clock) : ICommandHandler<CompleteStoreNotificationCommand>
{
    /// <inheritdoc />
    public async Task Handle(CompleteStoreNotificationCommand request, CancellationToken cancellationToken)
    {
        var receipt = await repository.GetAsync(request.StoreNotificationReceiptId, cancellationToken)
                      ?? throw new KnownException("STORE_NOTIFICATION_NOT_FOUND");
        receipt.Complete(leaseOwner: request.LeaseOwner, now: clock.UtcNow);
    }
}

/// <summary>
/// 商店通知完成命令锁。
/// </summary>
public class CompleteStoreNotificationCommandLock : ICommandLock<CompleteStoreNotificationCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        CompleteStoreNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-notification-id:{command.StoreNotificationReceiptId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 完成商店通知处理命令验证器。
/// </summary>
public class CompleteStoreNotificationCommandValidator : AbstractValidator<CompleteStoreNotificationCommand>
{
    public CompleteStoreNotificationCommandValidator()
    {
        RuleFor(x => x.LeaseOwner).NotEmpty().MaximumLength(255);
    }
}
