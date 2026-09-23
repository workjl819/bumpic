using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreNotification;

/// <summary>
/// 领取商店通知处理租约命令。
/// </summary>
public record AcquireStoreNotificationCommand(
    StoreNotificationReceiptId StoreNotificationReceiptId,
    string LeaseOwner,
    TimeSpan LeaseDuration) : ICommand<StoreNotificationReceiptAcquireResult>;

/// <summary>
/// 领取商店通知处理租约命令处理器。
/// </summary>
public class AcquireStoreNotificationCommandHandler(
    IStoreNotificationReceiptRepository repository,
    IClock clock) : ICommandHandler<AcquireStoreNotificationCommand, StoreNotificationReceiptAcquireResult>
{
    /// <inheritdoc />
    public async Task<StoreNotificationReceiptAcquireResult> Handle(
        AcquireStoreNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var receipt = await repository.GetAsync(request.StoreNotificationReceiptId, cancellationToken)
                      ?? throw new KnownException("STORE_NOTIFICATION_NOT_FOUND");
        return receipt.TryAcquire(
            leaseOwner: request.LeaseOwner,
            leaseDuration: request.LeaseDuration,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 商店通知租约领取命令锁。
/// </summary>
public class AcquireStoreNotificationCommandLock : ICommandLock<AcquireStoreNotificationCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        AcquireStoreNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-notification-id:{command.StoreNotificationReceiptId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 领取商店通知处理租约命令验证器。
/// </summary>
public class AcquireStoreNotificationCommandValidator : AbstractValidator<AcquireStoreNotificationCommand>
{
    public AcquireStoreNotificationCommandValidator()
    {
        RuleFor(x => x.LeaseOwner).NotEmpty().MaximumLength(255);
        RuleFor(x => x.LeaseDuration).GreaterThan(TimeSpan.Zero).LessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }
}
