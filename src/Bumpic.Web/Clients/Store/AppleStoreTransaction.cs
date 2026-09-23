namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 已通过签名和部署校验的 Apple 权威交易快照。
/// </summary>
public record AppleStoreTransaction(
    string TransactionId,
    string ProductId,
    string BundleId,
    string Environment,
    string TransactionType,
    int Quantity,
    DateTimeOffset PurchasedAt,
    DateTimeOffset SignedDate,
    string? AppAccountToken,
    decimal? Amount,
    string? CurrencyCode,
    bool IsRevoked,
    DateTimeOffset? RevocationDate,
    string PayloadHash);
