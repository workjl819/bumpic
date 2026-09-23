using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 应用 Apple 退款撤回命令。
/// </summary>
public record ReinstateStorePurchaseCommand(
    StoreTransactionId StoreTransactionId,
    DateTimeOffset PlatformVersionAt,
    string FactKey) : ICommand<int>;

/// <summary>
/// 应用 Apple 退款撤回命令处理器。
/// </summary>
public class ReinstateStorePurchaseCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<ReinstateStorePurchaseCommand, int>
{
    /// <summary>
    /// 恢复当前累计冲正点数并由领域事件协调点数入账。
    /// </summary>
    public async Task<int> Handle(
        ReinstateStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(request.StoreTransactionId, cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        return transaction.ReinstateAppleRefund(
            platformVersionAt: request.PlatformVersionAt,
            factKey: request.FactKey,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 应用 Apple 退款撤回命令锁。
/// </summary>
public class ReinstateStorePurchaseCommandLock : ICommandLock<ReinstateStorePurchaseCommand>
{
    /// <summary>
    /// 同一商店交易的状态变更串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReinstateStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-transaction:{command.StoreTransactionId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 应用 Apple 退款撤回命令验证器。
/// </summary>
public class ReinstateStorePurchaseCommandValidator : AbstractValidator<ReinstateStorePurchaseCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public ReinstateStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).NotNull();
        RuleFor(x => x.FactKey).NotEmpty();
    }
}
