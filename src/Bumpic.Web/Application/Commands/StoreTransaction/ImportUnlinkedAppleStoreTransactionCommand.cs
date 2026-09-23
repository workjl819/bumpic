using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using StoreTransactionEntity = Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransaction;
using System.Security.Cryptography;
using System.Text;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 从可信通知导入未归属 Apple 交易命令。
/// </summary>
public record ImportUnlinkedAppleStoreTransactionCommand(
    string ExternalTransactionId) : ICommand<StoreTransactionId>;

/// <summary>
/// 从可信通知导入未归属 Apple 交易命令处理器。
/// </summary>
public class ImportUnlinkedAppleStoreTransactionCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<ImportUnlinkedAppleStoreTransactionCommand, StoreTransactionId>
{
    /// <inheritdoc />
    public async Task<StoreTransactionId> Handle(
        ImportUnlinkedAppleStoreTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindByExternalTransactionIdAsync(
            store: AppStore.AppleAppStore,
            externalTransactionId: request.ExternalTransactionId,
            cancellationToken: cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var transaction = StoreTransactionEntity.CreateUnlinkedApple(
            externalTransactionId: request.ExternalTransactionId,
            now: clock.UtcNow);
        await repository.AddAsync(transaction, cancellationToken);
        return transaction.Id;
    }
}

/// <summary>
/// 从通知导入 Apple 交易命令锁。
/// </summary>
public class ImportUnlinkedAppleStoreTransactionCommandLock
    : ICommandLock<ImportUnlinkedAppleStoreTransactionCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        ImportUnlinkedAppleStoreTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        var identity = $"{AppStore.AppleAppStore}:{command.ExternalTransactionId}";
        var transactionKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-verification:{transactionKey}",
            acquireSeconds: 30));
    }
}

/// <summary>
/// 从可信通知导入未归属 Apple 交易命令验证器。
/// </summary>
public class ImportUnlinkedAppleStoreTransactionCommandValidator
    : AbstractValidator<ImportUnlinkedAppleStoreTransactionCommand>
{
    public ImportUnlinkedAppleStoreTransactionCommandValidator()
    {
        RuleFor(x => x.ExternalTransactionId).NotEmpty().MaximumLength(1000);
    }
}
