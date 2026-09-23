using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.StoreTransaction;

/// <summary>
/// 等待平台补偿验证的商店交易。
/// </summary>
public record PendingVerificationStoreTransactionResult(
    StoreTransactionId StoreTransactionId,
    AppStore Store,
    string ExternalTransactionId,
    DateTimeOffset CreatedAt);
