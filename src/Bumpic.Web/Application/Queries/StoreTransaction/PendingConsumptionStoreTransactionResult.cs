using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;

namespace Bumpic.Web.Application.Queries.StoreTransaction;

/// <summary>
/// Google Play 待消费交易补偿候选。
/// </summary>
public record PendingConsumptionStoreTransactionResult(
    StoreTransactionId StoreTransactionId,
    string PurchaseToken,
    string ProductId,
    int ConsumptionAttemptCount,
    DateTimeOffset CreatedAt);
