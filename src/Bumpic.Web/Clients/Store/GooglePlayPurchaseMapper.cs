using System.Security.Cryptography;
using Google.Apis.AndroidPublisher.v3.Data;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 将 Play Developer API 的 ProductPurchaseV2 快照映射为领域可用的购买快照。
/// </summary>
public static class GooglePlayPurchaseMapper
{
    private const string ConsumedState = "CONSUMPTION_STATE_CONSUMED";
    private const string YetToBeConsumedState = "CONSUMPTION_STATE_YET_TO_BE_CONSUMED";
    private const string AcknowledgedState = "ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED";

    /// <summary>
    /// 映射权威购买快照；商品标识缺失或多商品行时视为无效结果。
    /// </summary>
    public static GooglePlayPurchase Map(ProductPurchaseV2 purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        var lineItem = ReadSingleLineItem(purchase: purchase);
        var offerDetails = lineItem.ProductOfferDetails;
        var quantity = offerDetails?.Quantity ?? 1;
        var refundableQuantity = offerDetails?.RefundableQuantity ?? quantity;
        var purchaseState = ParsePurchaseState(
            value: purchase.PurchaseStateContext?.PurchaseState);
        var isConsumed = ParseConsumptionState(value: offerDetails?.ConsumptionState);
        if (quantity <= 0 || refundableQuantity < 0 || refundableQuantity > quantity)
        {
            throw Invalid(message: "Google Play 购买快照中的数量不合法。");
        }
        var purchaseCompletedAt = purchase.PurchaseCompletionTimeDateTimeOffset;
        var isTestPurchase = purchase.TestPurchaseContext is not null;
        var isAcknowledged = string.Equals(
            purchase.AcknowledgementState,
            AcknowledgedState,
            StringComparison.Ordinal);

        return new GooglePlayPurchase(
            ProductId: lineItem.ProductId!,
            PurchaseState: purchaseState,
            IsConsumed: isConsumed,
            Quantity: quantity,
            RefundableQuantity: refundableQuantity,
            ObfuscatedExternalAccountId: purchase.ObfuscatedExternalAccountId,
            OrderId: purchase.OrderId,
            PurchaseCompletedAt: purchaseCompletedAt,
            IsTestPurchase: isTestPurchase,
            IsAcknowledged: isAcknowledged,
            SnapshotHash: ComputeSnapshotHash(
                productId: lineItem.ProductId!,
                purchaseState: purchaseState,
                isConsumed: isConsumed,
                quantity: quantity,
                refundableQuantity: refundableQuantity,
                obfuscatedExternalAccountId: purchase.ObfuscatedExternalAccountId,
                orderId: purchase.OrderId,
                purchaseCompletedAt: purchaseCompletedAt,
                isTestPurchase: isTestPurchase,
                isAcknowledged: isAcknowledged));
    }

    private static ProductLineItem ReadSingleLineItem(ProductPurchaseV2 purchase)
    {
        var lineItems = purchase.ProductLineItem;
        if (lineItems is null || lineItems.Count != 1)
        {
            throw Invalid(message: "Google Play 购买快照必须且只能包含一个商品行。");
        }

        var lineItem = lineItems[0];
        if (string.IsNullOrWhiteSpace(lineItem.ProductId))
        {
            throw Invalid(message: "Google Play 购买快照缺少商品标识。");
        }

        return lineItem;
    }

    private static bool ParseConsumptionState(string? value)
    {
        return value switch
        {
            ConsumedState => true,
            YetToBeConsumedState => false,
            _ => throw Invalid(message: $"Google Play 消费状态 {value} 暂不识别。")
        };
    }

    private static GooglePlayPurchaseState ParsePurchaseState(string? value)
    {
        return value switch
        {
            "PURCHASED" or "PURCHASE_STATE_PURCHASED" => GooglePlayPurchaseState.Purchased,
            "PENDING" or "PURCHASE_STATE_PENDING" => GooglePlayPurchaseState.Pending,
            "CANCELED" or "CANCELLED" or "PURCHASE_STATE_CANCELED" => GooglePlayPurchaseState.Canceled,
            _ => throw Invalid(message: $"Google Play 购买状态 {value} 暂不识别。")
        };
    }

    private static string ComputeSnapshotHash(
        string productId,
        GooglePlayPurchaseState purchaseState,
        bool isConsumed,
        int quantity,
        int refundableQuantity,
        string? obfuscatedExternalAccountId,
        string? orderId,
        DateTimeOffset? purchaseCompletedAt,
        bool isTestPurchase,
        bool isAcknowledged)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        CanonicalEncoding.WriteString(payload, productId);
        CanonicalEncoding.WriteString(payload, purchaseState.ToString());
        CanonicalEncoding.WriteBoolean(payload, isConsumed);
        CanonicalEncoding.WriteInt32(payload, quantity);
        CanonicalEncoding.WriteInt32(payload, refundableQuantity);
        CanonicalEncoding.WriteNullableString(payload, obfuscatedExternalAccountId);
        CanonicalEncoding.WriteNullableString(payload, orderId);
        CanonicalEncoding.WriteNullableTimestamp(payload, purchaseCompletedAt);
        CanonicalEncoding.WriteBoolean(payload, isTestPurchase);
        CanonicalEncoding.WriteBoolean(payload, isAcknowledged);
        return CanonicalEncoding.EncodeBase64Url(SHA256.HashData(payload.ToArray()));
    }

    private static StoreClientException Invalid(string message)
    {
        return new StoreClientException(
            code: "PURCHASE_INVALID",
            isRetryable: false,
            message: message);
    }
}
