namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Google Play 权威购买快照。
/// </summary>
public record GooglePlayPurchase(
    string ProductId,
    GooglePlayPurchaseState PurchaseState,
    bool IsConsumed,
    int Quantity,
    int RefundableQuantity,
    string? ObfuscatedExternalAccountId,
    string? OrderId,
    DateTimeOffset? PurchaseCompletedAt,
    bool IsTestPurchase,
    bool IsAcknowledged,
    string SnapshotHash);
