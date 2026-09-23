using System.Text.Json;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 已完成验签的 Apple 交易载荷字段视图。
/// </summary>
internal sealed record AppleTransactionPayload(
    string TransactionId,
    string ProductId,
    string BundleId,
    string Environment,
    string TransactionType,
    int Quantity,
    long PurchaseDate,
    long SignedDate,
    string? AppAccountToken,
    decimal? Amount,
    string? CurrencyCode,
    long RevocationDate)
{
    private const string ConsumableTransactionType = "Consumable";

    /// <summary>
    /// 从已验证的 Apple 交易载荷读取字段并校验必填项。
    /// </summary>
    internal static AppleTransactionPayload Read(JsonElement payload)
    {
        var transactionId = RequireValue(
            value: ReadOptionalString(element: payload, propertyName: "transactionId"),
            fieldName: "transactionId");
        var transactionType = RequireValue(
            value: ReadOptionalString(element: payload, propertyName: "type"),
            fieldName: "type");
        if (!string.Equals(transactionType, ConsumableTransactionType, StringComparison.Ordinal))
        {
            throw Invalid(message: "Apple 商品类型不是消耗型商品。");
        }

        var purchaseDate = ReadOptionalInt64(element: payload, propertyName: "purchaseDate") ?? 0;
        if (purchaseDate <= 0)
        {
            throw Invalid(message: "Apple 签名交易缺少购买时间。");
        }

        var signedDate = ReadOptionalInt64(element: payload, propertyName: "signedDate") ?? 0;
        if (signedDate <= 0)
        {
            throw Invalid(message: "Apple 签名交易缺少签名时间。");
        }

        var price = ReadOptionalInt64(element: payload, propertyName: "price");
        return new AppleTransactionPayload(
            TransactionId: transactionId,
            ProductId: RequireValue(
                value: ReadOptionalString(element: payload, propertyName: "productId"),
                fieldName: "productId"),
            BundleId: RequireValue(
                value: ReadOptionalString(element: payload, propertyName: "bundleId"),
                fieldName: "bundleId"),
            Environment: RequireValue(
                value: ReadOptionalString(element: payload, propertyName: "environment"),
                fieldName: "environment"),
            TransactionType: transactionType,
            Quantity: ReadOptionalInt32(element: payload, propertyName: "quantity") ?? 1,
            PurchaseDate: purchaseDate,
            SignedDate: signedDate,
            AppAccountToken: ReadOptionalString(element: payload, propertyName: "appAccountToken"),
            Amount: price is null or <= 0 ? null : price.Value / 1000m,
            CurrencyCode: ReadOptionalString(element: payload, propertyName: "currency"),
            RevocationDate: ReadOptionalInt64(element: payload, propertyName: "revocationDate") ?? 0);
    }

    /// <summary>
    /// 校验客户端 JWS 与服务端权威交易的身份字段一致。
    /// </summary>
    internal void EnsureMatchesAuthoritativeTransaction(AppleTransactionPayload authoritative)
    {
        ArgumentNullException.ThrowIfNull(authoritative);
        EnsureFieldMatches(
            fieldName: "交易标识",
            clientValue: TransactionId,
            authoritativeValue: authoritative.TransactionId);
        EnsureFieldMatches(
            fieldName: "应用",
            clientValue: BundleId,
            authoritativeValue: authoritative.BundleId);
        EnsureFieldMatches(
            fieldName: "商品",
            clientValue: ProductId,
            authoritativeValue: authoritative.ProductId);
        EnsureFieldMatches(
            fieldName: "环境",
            clientValue: Environment,
            authoritativeValue: authoritative.Environment);
        EnsureFieldMatches(
            fieldName: "账户绑定标识",
            clientValue: AppAccountToken ?? string.Empty,
            authoritativeValue: authoritative.AppAccountToken ?? string.Empty,
            comparison: StringComparison.OrdinalIgnoreCase);
        EnsureFieldMatches(
            fieldName: "交易类型",
            clientValue: TransactionType,
            authoritativeValue: authoritative.TransactionType);
        if (Quantity != authoritative.Quantity)
        {
            throw Invalid(message: "客户端交易与服务端权威交易不一致：购买数量。");
        }

        // 退款、撤销和交易原因属于商店后续状态，以服务端权威结果为准，不做严格比对。
    }

    /// <summary>
    /// 使用权威交易字段构建验单快照。
    /// </summary>
    internal AppleStoreTransaction CreateSnapshot(string payloadHash)
    {
        var isRevoked = RevocationDate > 0;
        return new AppleStoreTransaction(
            TransactionId: TransactionId,
            ProductId: ProductId,
            BundleId: BundleId,
            Environment: Environment,
            TransactionType: TransactionType,
            Quantity: Quantity <= 0 ? 1 : Quantity,
            PurchasedAt: DateTimeOffset.FromUnixTimeMilliseconds(PurchaseDate),
            SignedDate: DateTimeOffset.FromUnixTimeMilliseconds(SignedDate),
            AppAccountToken: AppAccountToken,
            Amount: Amount,
            CurrencyCode: CurrencyCode,
            IsRevoked: isRevoked,
            RevocationDate: isRevoked
                ? DateTimeOffset.FromUnixTimeMilliseconds(RevocationDate)
                : null,
            PayloadHash: payloadHash);
    }

    private static void EnsureFieldMatches(
        string fieldName,
        string clientValue,
        string authoritativeValue,
        StringComparison comparison = StringComparison.Ordinal)
    {
        if (!string.Equals(clientValue, authoritativeValue, comparison))
        {
            throw Invalid(message: $"客户端交易与服务端权威交易不一致：{fieldName}。");
        }
    }

    private static string RequireValue(string? value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(message: $"Apple 签名交易缺少 {fieldName}。")
            : value;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? ReadOptionalInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static int? ReadOptionalInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static StoreClientException Invalid(string message)
    {
        return new StoreClientException(
            code: "PURCHASE_INVALID",
            isRetryable: false,
            message: message);
    }
}
