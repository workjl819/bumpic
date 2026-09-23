using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.PointAccount;

/// <summary>
/// 冲正购买点数命令。
/// </summary>
public record ReversePurchasedPointsCommand(
    UserAccountId UserAccountId,
    StoreTransactionId StoreTransactionId,
    string FactKey,
    int Points) : ICommand<bool>;

/// <summary>
/// 冲正购买点数命令处理器。
/// </summary>
public class ReversePurchasedPointsCommandHandler(
    IPointAccountRepository repository,
    IAccountPointRecordRepository pointRecordRepository,
    IClock clock) : ICommandHandler<ReversePurchasedPointsCommand, bool>
{
    /// <summary>
    /// 扣回点数并追加不可变退款流水。
    /// </summary>
    public async Task<bool> Handle(
        ReversePurchasedPointsCommand request,
        CancellationToken cancellationToken)
    {
        var businessReference = $"REFUND:{request.StoreTransactionId.Id:D}:{request.FactKey}";
        if (await pointRecordRepository.ExistsAsync(
                AccountPointRecordType.PurchaseReversed,
                businessReference,
                cancellationToken))
        {
            return false;
        }

        var account = await repository.GetByUserAccountIdAsync(
            userAccountId: request.UserAccountId,
            cancellationToken: cancellationToken);
        if (account is null)
        {
            account = Domain.AggregateModel.PointAccountAggregate.PointAccount.Create(request.UserAccountId);
            await repository.AddAsync(account, cancellationToken);
        }

        return account.ReversePurchasedPoints(
            points: request.Points,
            businessReference: businessReference,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 冲正购买点数命令锁。
/// </summary>
public class ReversePurchasedPointsCommandLock : ICommandLock<ReversePurchasedPointsCommand>
{
    /// <summary>
    /// 同一用户的点数账户变更串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReversePurchasedPointsCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:point-account:{command.UserAccountId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 冲正购买点数命令验证器。
/// </summary>
public class ReversePurchasedPointsCommandValidator : AbstractValidator<ReversePurchasedPointsCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public ReversePurchasedPointsCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).Must(x => x.Id != Guid.Empty);
        RuleFor(x => x.FactKey).NotEmpty();
        RuleFor(x => x.Points).GreaterThan(0);
    }
}
