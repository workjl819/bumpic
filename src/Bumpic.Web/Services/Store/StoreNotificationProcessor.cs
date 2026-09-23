using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransaction;
using Bumpic.Web.Application.Commands.StoreTransactionFact;
using Bumpic.Web.Application.Queries.StoreNotification;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Services.Store;

/// <summary>
/// 解析 durable inbox 并通过 Command 合并交易状态的处理器。
/// </summary>
public class StoreNotificationProcessor(
    IMediator mediator,
    IAppleSignedPayloadVerifier appleSignedPayloadVerifier,
    IGooglePlayClient googlePlayClient,
    IOptions<AppleStoreOptions> appleOptions) : IStoreNotificationProcessor
{
    /// <inheritdoc />
    public async Task ProcessAsync(
        Domain.AggregateModel.StoreNotificationReceiptAggregate.StoreNotificationReceiptId receiptId,
        CancellationToken cancellationToken)
    {
        var receipt = await mediator.Send(
            new GetStoreNotificationReceiptQuery(StoreNotificationReceiptId: receiptId),
            cancellationToken)
                      ?? throw new StoreNotificationProcessingException(
                          code: "STORE_NOTIFICATION_PAYLOAD_MISSING",
                          isRetryable: false,
                          message: "通知不存在或没有可处理的规范化载荷。");
        if (receipt.Store == AppStore.GooglePlay)
        {
            await ProcessGoogleAsync(receipt: receipt, cancellationToken: cancellationToken);
            return;
        }

        await ProcessAppleAsync(receipt: receipt, cancellationToken: cancellationToken);
    }

    private async Task ProcessGoogleAsync(
        GetStoreNotificationReceiptResult receipt,
        CancellationToken cancellationToken)
    {
        if (receipt.NotificationType is "TEST" or "PENDING_REFUND_REVIEW")
        {
            return;
        }

        if (receipt.NotificationType is not (
                "ONE_TIME_PRODUCT_PURCHASED"
                or "ONE_TIME_PRODUCT_CANCELED"
                or "VOIDED_PURCHASE"))
        {
            throw UnsupportedGoogleNotification(
                code: "STORE_NOTIFICATION_TYPE_UNSUPPORTED",
                message: $"Google 通知类型 {receipt.NotificationType} 尚未支持。");
        }

        using var document = JsonDocument.Parse(receipt.NormalizedPayload);
        var payload = ReadRequiredObject(document.RootElement, "payload");
        var notification = ReadGoogleNotification(
            payload: payload,
            notificationType: receipt.NotificationType);
        GooglePlayPurchase purchase;
        try
        {
            purchase = await googlePlayClient.GetPurchaseAsync(
                new GooglePlayPurchaseRequest(PurchaseToken: notification.PurchaseToken),
                cancellationToken);
        }
        catch (StoreClientException exception)
        {
            throw new StoreNotificationProcessingException(
                code: exception.Code,
                isRetryable: exception.IsRetryable,
                message: exception.Message);
        }

        ValidateGooglePurchaseSnapshot(notification: notification, purchase: purchase);
        var storeTransactionId = await mediator.Send(
            new ImportUnlinkedGoogleStoreTransactionCommand(
                ExternalTransactionId: notification.PurchaseToken,
                IsTestPurchase: purchase.IsTestPurchase),
            cancellationToken);

        if (receipt.NotificationType == "ONE_TIME_PRODUCT_CANCELED")
        {
            if (purchase.PurchaseState != GooglePlayPurchaseState.Canceled)
            {
                throw new StoreNotificationProcessingException(
                    code: "GOOGLE_CANCELLATION_NOT_YET_VISIBLE",
                    isRetryable: true,
                    message: "Google Play 延迟付款取消尚未出现在 ProductPurchaseV2 权威快照中。");
            }

            await ApplyGoogleFactAsync(
                receipt: receipt,
                notification: notification,
                purchase: purchase,
                storeTransactionId: storeTransactionId,
                factType: StoreTransactionFactType.Canceled,
                applyCommand: _ => mediator.Send(new CancelGoogleStorePurchaseCommand(
                    StoreTransactionId: storeTransactionId), cancellationToken),
                cancellationToken: cancellationToken);
            return;
        }

        if (purchase.RefundableQuantity < purchase.Quantity)
        {
            var factType = receipt.NotificationType == "VOIDED_PURCHASE"
                ? StoreTransactionFactType.Voided
                : StoreTransactionFactType.Refunded;
            await ApplyGoogleFactAsync(
                receipt: receipt,
                notification: notification,
                purchase: purchase,
                storeTransactionId: storeTransactionId,
                factType: factType,
                applyCommand: factKey => mediator.Send(new ReverseGoogleStorePurchaseCommand(
                    StoreTransactionId: storeTransactionId,
                    RefundedAt: receipt.OccurredAt,
                    FactKey: factKey), cancellationToken),
                cancellationToken: cancellationToken);
            return;
        }

        if (receipt.NotificationType == "VOIDED_PURCHASE")
        {
            throw new StoreNotificationProcessingException(
                code: "GOOGLE_REFUND_NOT_YET_VISIBLE",
                isRetryable: true,
                message: "Google Play 作废通知对应的退款尚未出现在 ProductPurchaseV2 权威快照中。");
        }

        if (purchase.PurchaseState == GooglePlayPurchaseState.Canceled)
        {
            await ApplyGoogleFactAsync(
                receipt: receipt,
                notification: notification,
                purchase: purchase,
                storeTransactionId: storeTransactionId,
                factType: StoreTransactionFactType.Canceled,
                applyCommand: _ => mediator.Send(new CancelGoogleStorePurchaseCommand(
                    StoreTransactionId: storeTransactionId), cancellationToken),
                cancellationToken: cancellationToken);
            return;
        }

        if (purchase.PurchaseState != GooglePlayPurchaseState.Purchased)
        {
            throw new StoreNotificationProcessingException(
                code: "GOOGLE_PURCHASE_NOT_YET_FINAL",
                isRetryable: true,
                message: "Google Play 通知对应的购买状态尚未收敛，稍后重新查询权威快照。");
        }

        if (!purchase.PurchaseCompletedAt.HasValue)
        {
            throw new StoreNotificationProcessingException(
                code: "GOOGLE_PURCHASE_NOT_YET_FINAL",
                isRetryable: true,
                message: "Google Play 购买完成时间尚未出现在 ProductPurchaseV2 权威快照中。");
        }

        await ApplyGoogleFactAsync(
            receipt: receipt,
            notification: notification,
            purchase: purchase,
            storeTransactionId: storeTransactionId,
            factType: StoreTransactionFactType.Purchased,
            applyCommand: async _ =>
            {
                var result = await mediator.Send(new ReconcileGoogleStorePurchaseCommand(
                    StoreTransactionId: storeTransactionId,
                    PurchaseToken: notification.PurchaseToken,
                    Purchase: purchase), cancellationToken);
                if (result.Outcome is ReconcileGoogleStorePurchaseOutcome.RetryableFailed
                    or ReconcileGoogleStorePurchaseOutcome.DeterministicFailed)
                {
                    throw new StoreNotificationProcessingException(
                        code: result.FailureCode ?? "GOOGLE_PURCHASE_RECONCILIATION_FAILED",
                        isRetryable: result.Outcome == ReconcileGoogleStorePurchaseOutcome.RetryableFailed,
                        message: "Google Play 通知购买无法完成消费和入账。");
                }
            },
            cancellationToken: cancellationToken);
    }

    private async Task ApplyGoogleFactAsync(
        GetStoreNotificationReceiptResult receipt,
        GooglePlayNotificationData notification,
        GooglePlayPurchase purchase,
        Domain.AggregateModel.StoreTransactionAggregate.StoreTransactionId storeTransactionId,
        StoreTransactionFactType factType,
        Func<string, Task> applyCommand,
        CancellationToken cancellationToken)
    {
        var factKey = BuildFactKey(
            store: AppStore.GooglePlay,
            externalNotificationId: receipt.ExternalNotificationId,
            transactionId: notification.PurchaseToken,
            factType: factType,
            occurredAt: receipt.OccurredAt);
        var payloadHash = BuildGoogleEvidenceHash(
            notificationPayloadHash: receipt.PayloadHash,
            snapshotHash: purchase.SnapshotHash);
        var recordCommand = new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: receipt.StoreNotificationReceiptId,
            SourceType: StoreTransactionFactSourceType.Notification,
            Store: AppStore.GooglePlay,
            ExternalTransactionId: notification.PurchaseToken,
            StoreOrderId: purchase.OrderId ?? notification.OrderId,
            ExternalEventId: receipt.ExternalNotificationId,
            Type: factType,
            OccurredAt: receipt.OccurredAt,
            PlatformVersionAt: null,
            FactKey: factKey,
            PayloadHash: payloadHash,
            AppliedStoreTransactionId: null);
        await mediator.Send(recordCommand, cancellationToken);
        await applyCommand(Convert.ToHexString(factKey));
        await mediator.Send(recordCommand with
        {
            AppliedStoreTransactionId = storeTransactionId
        }, cancellationToken);
    }

    private static GooglePlayNotificationData ReadGoogleNotification(
        JsonElement payload,
        string notificationType)
    {
        if (notificationType == "VOIDED_PURCHASE")
        {
            var value = ReadRequiredObject(payload, "voidedPurchaseNotification");
            var productType = ReadOptionalInt32(value, "productType");
            if (productType.HasValue && productType.Value != 2)
            {
                throw UnsupportedGoogleNotification(
                    code: "GOOGLE_PRODUCT_TYPE_UNSUPPORTED",
                    message: "当前服务只处理 Google Play 一次性商品作废通知。");
            }

            var refundType = ReadOptionalInt32(value, "refundType");
            if (refundType == 2)
            {
                throw UnsupportedGoogleNotification(
                    code: "GOOGLE_PARTIAL_REFUND_UNSUPPORTED",
                    message: "P0 不支持 Google Play 多数量或按数量部分退款。");
            }

            if (refundType.HasValue && refundType.Value != 1)
            {
                throw UnsupportedGoogleNotification(
                    code: "GOOGLE_REFUND_TYPE_UNSUPPORTED",
                    message: "Google Play 作废通知包含未支持的退款类型。");
            }

            return new GooglePlayNotificationData(
                PurchaseToken: ReadRequiredGoogleString(value, "purchaseToken", "Google 作废通知"),
                ProductId: null,
                OrderId: ReadOptionalString(value, "orderId"));
        }

        var oneTime = ReadRequiredObject(payload, "oneTimeProductNotification");
        return new GooglePlayNotificationData(
            PurchaseToken: ReadRequiredGoogleString(oneTime, "purchaseToken", "Google 一次性商品通知"),
            ProductId: ReadRequiredGoogleString(oneTime, "sku", "Google 一次性商品通知"),
            OrderId: null);
    }

    private static void ValidateGooglePurchaseSnapshot(
        GooglePlayNotificationData notification,
        GooglePlayPurchase purchase)
    {
        if (purchase.Quantity != 1)
        {
            throw UnsupportedGoogleNotification(
                code: "GOOGLE_MULTI_QUANTITY_UNSUPPORTED",
                message: "P0 不支持 Google Play 多数量购买及其退款。");
        }

        if (notification.ProductId is not null
            && !string.Equals(notification.ProductId, purchase.ProductId, StringComparison.Ordinal))
        {
            throw UnsupportedGoogleNotification(
                code: "GOOGLE_PRODUCT_MISMATCH",
                message: "Google RTDN 商品与 ProductPurchaseV2 权威商品不一致。");
        }
    }

    private static JsonElement ReadRequiredObject(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        throw UnsupportedGoogleNotification(
            code: "STORE_NOTIFICATION_SCHEMA_UNSUPPORTED",
            message: $"Google 通知缺少 {propertyName}。");
    }

    private static string ReadRequiredGoogleString(
        JsonElement element,
        string propertyName,
        string sourceName)
    {
        var value = ReadOptionalString(element: element, propertyName: propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw UnsupportedGoogleNotification(
            code: "STORE_NOTIFICATION_SCHEMA_UNSUPPORTED",
            message: $"{sourceName}缺少 {propertyName}。");
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadOptionalInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
        {
            return numeric;
        }

        return value.ValueKind == JsonValueKind.String
               && int.TryParse(value.GetString(), out var textValue)
            ? textValue
            : null;
    }

    private static StoreNotificationProcessingException UnsupportedGoogleNotification(
        string code,
        string message)
    {
        return new StoreNotificationProcessingException(
            code: code,
            isRetryable: false,
            message: message);
    }

    private static byte[] BuildGoogleEvidenceHash(byte[] notificationPayloadHash, string snapshotHash)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        payload.Write(notificationPayloadHash);
        CanonicalEncoding.WriteString(payload, snapshotHash);
        return SHA256.HashData(payload.ToArray());
    }

    private async Task ProcessAppleAsync(
        GetStoreNotificationReceiptResult receipt,
        CancellationToken cancellationToken)
    {
        if (receipt.NotificationType is "TEST" or "CONSUMPTION_REQUEST" or "REFUND_DECLINED")
        {
            return;
        }

        if (receipt.NotificationType is not ("ONE_TIME_CHARGE" or "REFUND" or "REFUND_REVERSED"))
        {
            throw new StoreNotificationProcessingException(
                code: "STORE_NOTIFICATION_TYPE_UNSUPPORTED",
                isRetryable: false,
                message: $"Apple 通知类型 {receipt.NotificationType} 尚未支持。");
        }

        using var document = JsonDocument.Parse(receipt.NormalizedPayload);
        var transactionJws = ReadSignedTransactionInfo(root: document.RootElement);
        var signedTransaction = (await appleSignedPayloadVerifier.VerifyTransactionPayloadAsync(
            signedPayload: transactionJws,
            cancellationToken: cancellationToken)).Payload;
        ValidateAppleTransactionDeployment(payload: signedTransaction);
        var transactionId = ReadRequiredString(element: signedTransaction, propertyName: "transactionId");
        var platformVersionAt = ReadTimestamp(element: signedTransaction, propertyName: "signedDate")
                                ?? receipt.OccurredAt;

        if (receipt.NotificationType == "ONE_TIME_CHARGE")
        {
            await ProcessApplePurchaseAsync(
                receipt: receipt,
                signedTransaction: signedTransaction,
                transactionId: transactionId,
                platformVersionAt: platformVersionAt,
                cancellationToken: cancellationToken);
            return;
        }

        var occurredAt = ReadTimestamp(element: signedTransaction, propertyName: "revocationDate")
                         ?? receipt.OccurredAt;
        var factType = receipt.NotificationType == "REFUND"
            ? StoreTransactionFactType.Refunded
            : StoreTransactionFactType.RefundReversed;
        var factKey = BuildFactKey(
            store: AppStore.AppleAppStore,
            externalNotificationId: receipt.ExternalNotificationId,
            transactionId: transactionId,
            factType: factType,
            occurredAt: occurredAt);
        var storeTransactionId = await mediator.Send(
            new ImportUnlinkedAppleStoreTransactionCommand(
                ExternalTransactionId: transactionId),
            cancellationToken);
        await mediator.Send(new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: receipt.StoreNotificationReceiptId,
            SourceType: StoreTransactionFactSourceType.Notification,
            Store: AppStore.AppleAppStore,
            ExternalTransactionId: transactionId,
            StoreOrderId: null,
            ExternalEventId: receipt.ExternalNotificationId,
            Type: factType,
            OccurredAt: occurredAt,
            PlatformVersionAt: platformVersionAt,
            FactKey: factKey,
            PayloadHash: receipt.PayloadHash,
            AppliedStoreTransactionId: null), cancellationToken);

        var factKeyText = Convert.ToHexString(factKey);
        if (factType == StoreTransactionFactType.Refunded)
        {
            await mediator.Send(new ReverseStorePurchaseCommand(
                StoreTransactionId: storeTransactionId,
                RefundedAt: occurredAt,
                PlatformVersionAt: platformVersionAt,
                FactKey: factKeyText), cancellationToken);
        }
        else
        {
            await mediator.Send(new ReinstateStorePurchaseCommand(
                StoreTransactionId: storeTransactionId,
                PlatformVersionAt: platformVersionAt,
                FactKey: factKeyText), cancellationToken);
        }

        await mediator.Send(new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: receipt.StoreNotificationReceiptId,
            SourceType: StoreTransactionFactSourceType.Notification,
            Store: AppStore.AppleAppStore,
            ExternalTransactionId: transactionId,
            StoreOrderId: null,
            ExternalEventId: receipt.ExternalNotificationId,
            Type: factType,
            OccurredAt: occurredAt,
            PlatformVersionAt: platformVersionAt,
            FactKey: factKey,
            PayloadHash: receipt.PayloadHash,
            AppliedStoreTransactionId: storeTransactionId), cancellationToken);
    }

    private async Task ProcessApplePurchaseAsync(
        GetStoreNotificationReceiptResult receipt,
        JsonElement signedTransaction,
        string transactionId,
        DateTimeOffset platformVersionAt,
        CancellationToken cancellationToken)
    {
        var productId = ReadRequiredString(signedTransaction, "productId");
        var purchasedAt = ReadTimestamp(signedTransaction, "purchaseDate")
                          ?? throw new StoreNotificationProcessingException(
                              code: "STORE_NOTIFICATION_SCHEMA_UNSUPPORTED",
                              isRetryable: false,
                              message: "Apple 购买通知缺少 purchaseDate。");
        var transaction = new AppleStoreTransaction(
            TransactionId: transactionId,
            ProductId: productId,
            BundleId: ReadRequiredString(signedTransaction, "bundleId"),
            Environment: ReadRequiredString(signedTransaction, "environment"),
            TransactionType: ReadRequiredString(signedTransaction, "type"),
            Quantity: ReadOptionalInt32(signedTransaction, "quantity") ?? 1,
            PurchasedAt: purchasedAt,
            SignedDate: platformVersionAt,
            AppAccountToken: ReadOptionalString(signedTransaction, "appAccountToken"),
            Amount: ReadOptionalInt64(signedTransaction, "price") is { } price ? price / 1000m : null,
            CurrencyCode: ReadOptionalString(signedTransaction, "currency"),
            IsRevoked: false,
            RevocationDate: null,
            PayloadHash: Convert.ToHexString(receipt.PayloadHash));

        var storeTransactionId = await mediator.Send(
            new ImportUnlinkedAppleStoreTransactionCommand(transactionId),
            cancellationToken);
        var factKey = BuildFactKey(
            store: AppStore.AppleAppStore,
            externalNotificationId: receipt.ExternalNotificationId,
            transactionId: transactionId,
            factType: StoreTransactionFactType.Purchased,
            occurredAt: purchasedAt);
        var factCommand = new RecordStoreTransactionFactCommand(
            StoreNotificationReceiptId: receipt.StoreNotificationReceiptId,
            SourceType: StoreTransactionFactSourceType.Notification,
            Store: AppStore.AppleAppStore,
            ExternalTransactionId: transactionId,
            StoreOrderId: null,
            ExternalEventId: receipt.ExternalNotificationId,
            Type: StoreTransactionFactType.Purchased,
            OccurredAt: purchasedAt,
            PlatformVersionAt: platformVersionAt,
            FactKey: factKey,
            PayloadHash: receipt.PayloadHash,
            AppliedStoreTransactionId: null);
        await mediator.Send(factCommand, cancellationToken);

        var result = await mediator.Send(
            new ReconcileAppleStorePurchaseCommand(storeTransactionId, transaction),
            cancellationToken);
        if (result.Outcome is ReconcileAppleStorePurchaseOutcome.RetryableFailed
            or ReconcileAppleStorePurchaseOutcome.DeterministicFailed)
        {
            throw new StoreNotificationProcessingException(
                code: result.FailureCode ?? "APPLE_PURCHASE_RECONCILIATION_FAILED",
                isRetryable: result.Outcome == ReconcileAppleStorePurchaseOutcome.RetryableFailed,
                message: "Apple 通知购买无法完成归属和入账。");
        }

        await mediator.Send(factCommand with
        {
            AppliedStoreTransactionId = storeTransactionId
        }, cancellationToken);
    }

    private void ValidateAppleTransactionDeployment(JsonElement payload)
    {
        var configuration = appleOptions.Value;
        var environment = ReadRequiredString(element: payload, propertyName: "environment");
        if (!string.Equals(
                ReadRequiredString(element: payload, propertyName: "bundleId"),
                configuration.BundleId,
                StringComparison.Ordinal)
            || !string.Equals(
                environment,
                AppleStoreEnvironmentResolver.ResolveName(
                    environmentName: configuration.Environment),
                StringComparison.Ordinal))
        {
            throw new StoreNotificationProcessingException(
                code: "STORE_NOTIFICATION_DEPLOYMENT_MISMATCH",
                isRetryable: false,
                message: "Apple 签名交易不属于当前部署。");
        }

    }

    private static string ReadSignedTransactionInfo(JsonElement root)
    {
        if (root.TryGetProperty("payload", out var payload)
            && payload.TryGetProperty("data", out var data)
            && data.TryGetProperty("signedTransactionInfo", out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new StoreNotificationProcessingException(
            code: "STORE_NOTIFICATION_SCHEMA_UNSUPPORTED",
            isRetryable: false,
            message: "Apple 通知缺少 signedTransactionInfo。");
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new StoreNotificationProcessingException(
            code: "STORE_NOTIFICATION_SCHEMA_UNSUPPORTED",
            isRetryable: false,
            message: $"Apple 签名交易缺少 {propertyName}。");
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string propertyName)
    {
        var milliseconds = ReadOptionalInt64(element: element, propertyName: propertyName);
        return milliseconds.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value) : null;
    }

    private static long? ReadOptionalInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static byte[] BuildFactKey(
        AppStore store,
        string externalNotificationId,
        string transactionId,
        StoreTransactionFactType factType,
        DateTimeOffset occurredAt)
    {
        using var payload = new MemoryStream();
        CanonicalEncoding.WriteInt32(payload, CanonicalEncoding.Version);
        CanonicalEncoding.WriteString(payload, store.ToString());
        CanonicalEncoding.WriteString(payload, StoreTransactionFactSourceType.Notification.ToString());
        CanonicalEncoding.WriteString(payload, externalNotificationId);
        CanonicalEncoding.WriteString(payload, transactionId);
        CanonicalEncoding.WriteString(payload, factType.ToString());
        CanonicalEncoding.WriteTimestamp(payload, occurredAt);
        return SHA256.HashData(payload.ToArray());
    }
}
