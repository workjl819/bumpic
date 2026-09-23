using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.PointAccount;

/// <summary>
/// 恢复购买点数命令。
/// </summary>
public record ReinstatePurchasedPointsCommand(
    UserAccountId UserAccountId,
    StoreTransactionId StoreTransactionId,
    string FactKey,
    int Points) : ICommand<bool>;

/// <summary>
/// 恢复购买点数命令处理器。
/// </summary>
public class ReinstatePurchasedPointsCommandHandler(
    IPointAccountRepository repository,
    IAccountPointRecordRepository pointRecordRepository,
    IClock clock) : ICommandHandler<ReinstatePurchasedPointsCommand, bool>
{
    /// <summary>
    /// 恢复点数并追加不可变退款撤回流水。
    /// </summary>
    public async Task<bool> Handle(
        ReinstatePurchasedPointsCommand request,
        CancellationToken cancellationToken)
    {
        var businessReference = $"REFUND_REVERSED:{request.StoreTransactionId.Id:D}:{request.FactKey}";
        if (await pointRecordRepository.ExistsAsync(
                AccountPointRecordType.PurchaseReinstated,
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

        return account.ReinstatePurchasedPoints(
            points: request.Points,
            businessReference: businessReference,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 恢复购买点数命令锁。
/// </summary>
public class ReinstatePurchasedPointsCommandLock : ICommandLock<ReinstatePurchasedPointsCommand>
{
    /// <summary>
    /// 同一用户的点数账户变更串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        ReinstatePurchasedPointsCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:point-account:{command.UserAccountId.Id:N}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 恢复购买点数命令验证器。
/// </summary>
public class ReinstatePurchasedPointsCommandValidator : AbstractValidator<ReinstatePurchasedPointsCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public ReinstatePurchasedPointsCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).Must(x => x.Id != Guid.Empty);
        RuleFor(x => x.FactKey).NotEmpty();
        RuleFor(x => x.Points).GreaterThan(0);
    }
}
