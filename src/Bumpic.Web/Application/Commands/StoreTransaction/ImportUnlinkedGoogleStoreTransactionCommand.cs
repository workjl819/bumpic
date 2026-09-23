using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Infrastructure.Repositories;
using StoreTransactionEntity = Bumpic.Domain.AggregateModel.StoreTransactionAggregate.StoreTransaction;
using System.Security.Cryptography;
using System.Text;

namespace Bumpic.Web.Application.Commands.StoreTransaction;

/// <summary>
/// 从可信通知导入未归属 Google Play 交易命令。
/// </summary>
public record ImportUnlinkedGoogleStoreTransactionCommand(
    string ExternalTransactionId,
    bool IsTestPurchase) : ICommand<StoreTransactionId>;

/// <summary>
/// 从可信通知导入未归属 Google Play 交易命令处理器。
/// </summary>
public class ImportUnlinkedGoogleStoreTransactionCommandHandler(
    IStoreTransactionRepository repository,
    IClock clock) : ICommandHandler<ImportUnlinkedGoogleStoreTransactionCommand, StoreTransactionId>
{
    /// <inheritdoc />
    public async Task<StoreTransactionId> Handle(
        ImportUnlinkedGoogleStoreTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindByExternalTransactionIdAsync(
            store: AppStore.GooglePlay,
            externalTransactionId: request.ExternalTransactionId,
            cancellationToken: cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var transaction = StoreTransactionEntity.CreateUnlinkedGoogle(
            externalTransactionId: request.ExternalTransactionId,
            isTestPurchase: request.IsTestPurchase,
            now: clock.UtcNow);
        await repository.AddAsync(transaction, cancellationToken);
        return transaction.Id;
    }
}

/// <summary>
/// 从通知导入 Google Play 交易命令锁。
/// </summary>
public class ImportUnlinkedGoogleStoreTransactionCommandLock
    : ICommandLock<ImportUnlinkedGoogleStoreTransactionCommand>
{
    /// <inheritdoc />
    public Task<CommandLockSettings> GetLockKeysAsync(
        ImportUnlinkedGoogleStoreTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        var identity = $"{AppStore.GooglePlay}:{command.ExternalTransactionId}";
        var transactionKey = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Task.FromResult(new CommandLockSettings(
            lockKey: $"payment:store-verification:{transactionKey}",
            acquireSeconds: 10));
    }
}

/// <summary>
/// 从可信通知导入未归属 Google Play 交易命令验证器。
/// </summary>
public class ImportUnlinkedGoogleStoreTransactionCommandValidator
    : AbstractValidator<ImportUnlinkedGoogleStoreTransactionCommand>
{
    public ImportUnlinkedGoogleStoreTransactionCommandValidator()
    {
        RuleFor(x => x.ExternalTransactionId).NotEmpty().MaximumLength(1000);
    }
}
