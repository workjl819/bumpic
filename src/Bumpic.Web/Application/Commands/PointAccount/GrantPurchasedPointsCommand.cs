using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;

namespace Bumpic.Web.Application.Commands.PointAccount;

/// <summary>
/// 发放购买点数命令。
/// </summary>
public record GrantPurchasedPointsCommand(
    UserAccountId UserAccountId,
    StoreTransactionId StoreTransactionId,
    int Points) : ICommand<bool>;

/// <summary>
/// 发放购买点数命令处理器。
/// </summary>
public class GrantPurchasedPointsCommandHandler(
    IPointAccountRepository repository,
    IAccountPointRecordRepository pointRecordRepository,
    IClock clock) : ICommandHandler<GrantPurchasedPointsCommand, bool>
{
    /// <summary>
    /// 发放点数并追加不可变流水。
    /// </summary>
    public async Task<bool> Handle(
        GrantPurchasedPointsCommand request,
        CancellationToken cancellationToken)
    {
        var businessReference = $"PURCHASE:{request.StoreTransactionId.Id:D}";
        if (await pointRecordRepository.ExistsAsync(
                AccountPointRecordType.PurchaseGranted,
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

        return account.GrantPurchasedPoints(
            points: request.Points,
            businessReference: businessReference,
            now: clock.UtcNow);
    }
}

/// <summary>
/// 发放购买点数命令锁。
/// </summary>
public class GrantPurchasedPointsCommandLock : ICommandLock<GrantPurchasedPointsCommand>
{
    /// <summary>
    /// 同一用户的点数账户变更串行执行。
    /// </summary>
    public Task<CommandLockSettings> GetLockKeysAsync(
        GrantPurchasedPointsCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateSettings(userAccountId: command.UserAccountId.Id));
    }

    private static CommandLockSettings CreateSettings(Guid userAccountId)
    {
        return new CommandLockSettings(
            lockKey: $"payment:point-account:{userAccountId:N}",
            acquireSeconds: 10);
    }
}

/// <summary>
/// 发放购买点数命令验证器。
/// </summary>
public class GrantPurchasedPointsCommandValidator : AbstractValidator<GrantPurchasedPointsCommand>
{
    /// <summary>
    /// 初始化验证规则。
    /// </summary>
    public GrantPurchasedPointsCommandValidator()
    {
        RuleFor(x => x.StoreTransactionId).Must(x => x.Id != Guid.Empty);
        RuleFor(x => x.Points).GreaterThan(0);
    }
}
