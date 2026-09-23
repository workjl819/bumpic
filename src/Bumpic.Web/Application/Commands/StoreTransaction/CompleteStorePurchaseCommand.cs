using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 完成商店购买入账命令。
/// </summary>
public record CompleteStorePurchaseCommand(StoreTransactionId StoreTransactionId) : ICommand;

/// <summary>
/// 完成商店购买入账命令处理器。
/// </summary>
public class CompleteStorePurchaseCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<CompleteStorePurchaseCommand>
{
    /// <summary>
    /// 推进交易状态并由领域事件协调点数发放。
    /// </summary>
    public async Task Handle(
        CompleteStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(request.StoreTransactionId, cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        transaction.CompleteVerification(now: clock.UtcNow);
    }
}

/// <summary>
/// 完成商店购买入账命令锁。
/// </summary>
public class CompleteStorePurchaseCommandLock : ICommandLock<CompleteStorePurchaseCommand>
{
    /// <summary>
    /// 同一商店交易的状态变更串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        CompleteStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-transaction:{command.StoreTransactionId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 完成商店购买入账命令验证器。
/// </summary>
public class CompleteStorePurchaseCommandValidator : AbstractValidator<CompleteStorePurchaseCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public CompleteStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).NotNull();
    }
}
