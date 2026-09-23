using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 应用 Apple 权威退款命令。
/// </summary>
public record ReverseStorePurchaseCommand(
    StoreTransactionId StoreTransactionId,
    DateTimeOffset RefundedAt,
    DateTimeOffset PlatformVersionAt,
    string FactKey) : ICommand<int>;

/// <summary>
/// 应用 Apple 权威退款命令处理器。
/// </summary>
public class ReverseStorePurchaseCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<ReverseStorePurchaseCommand, int>
{
    /// <summary>
    /// 合并累计退款并由领域事件协调差额点数冲正。
    /// </summary>
    public async Task<int> Handle(
        ReverseStorePurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(request.StoreTransactionId, cancellationToken)
                          ?? throw new KnownException("PURCHASE_NOT_FOUND");
        return transaction.ApplyAppleRefund(
            refundedAt: request.RefundedAt,
            platformVersionAt: request.PlatformVersionAt,
            factKey: request.FactKey,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 应用 Apple 权威退款命令锁。
/// </summary>
public class ReverseStorePurchaseCommandLock : ICommandLock<ReverseStorePurchaseCommand>
{
    /// <summary>
    /// 同一商店交易的状态变更串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReverseStorePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-transaction:{command.StoreTransactionId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 应用 Apple 权威退款命令验证器。
/// </summary>
public class ReverseStorePurchaseCommandValidator : AbstractValidator<ReverseStorePurchaseCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public ReverseStorePurchaseCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).NotNull();
        RuleFor(x => x.FactKey).NotEmpty();
    }
}
