using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 应用 Google Play 权威延迟付款取消命令。
/// </summary>
public record CancelGoogleStorePurchaseCommand(
    StoreTransactionId StoreTransactionId) : ICommand;

/// <summary>
/// 应用 Google Play 权威延迟付款取消命令处理器。
/// </summary>
public class CancelGoogleStorePurchaseCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<CancelGoogleStorePurchaseCommand>
{
    /// <inheritdoc />
    public async Task Handle(
        CancelGoogleStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(request.StoreTransactionId, cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        transaction.ApplyGoogleCancellation(now: clock.UtcNow);
    }
}

/// <summary>
/// Google Play 延迟付款取消命令锁。
/// </summary>
public class CancelGoogleStorePurchaseCommandLock : ICommandLock<CancelGoogleStorePurchaseCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        CancelGoogleStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-transaction:{command.StoreTransactionId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// Google Play 延迟付款取消命令验证器。
/// </summary>
public class CancelGoogleStorePurchaseCommandValidator : AbstractValidator<CancelGoogleStorePurchaseCommand>
{
    public CancelGoogleStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).NotNull();
    }
}
