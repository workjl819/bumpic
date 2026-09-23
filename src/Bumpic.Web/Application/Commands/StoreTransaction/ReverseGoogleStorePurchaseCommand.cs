using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 应用 Google Play 权威全额退款或作废命令。
/// </summary>
public record ReverseGoogleStorePurchaseCommand(
    StoreTransactionId StoreTransactionId,
    DateTimeOffset RefundedAt,
    string FactKey) : ICommand<int>;

/// <summary>
/// 应用 Google Play 权威全额退款或作废命令处理器。
/// </summary>
public class ReverseGoogleStorePurchaseCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<ReverseGoogleStorePurchaseCommand, int>
{
    /// <inheritdoc />
    public async Task<int> Handle(
        ReverseGoogleStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(request.StoreTransactionId, cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        return transaction.ApplyGoogleFullRefund(
            refundedAt: request.RefundedAt,
            factKey: request.FactKey,
            now: clock.UtcNow);
    }
}

/// <summary>
/// Google Play 全额退款或作废命令锁。
/// </summary>
public class ReverseGoogleStorePurchaseCommandLock : ICommandLock<ReverseGoogleStorePurchaseCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReverseGoogleStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-transaction:{command.StoreTransactionId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// Google Play 全额退款或作废命令验证器。
/// </summary>
public class ReverseGoogleStorePurchaseCommandValidator : AbstractValidator<ReverseGoogleStorePurchaseCommand>
{
    public ReverseGoogleStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).NotNull();
        RuleFor(x => x.FactKey).NotEmpty();
    }
}
