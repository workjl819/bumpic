using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Bumpic.Domain.AggregateModel.StoreNotificationReceiptAggregate;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransaction;
using Bumpic.Web.Application.Commands.StoreTransactionFact;
using Bumpic.Web.Application.Queries.StoreNotification;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;
using Bumpic.Web.Services.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// durable inbox 商店通知业务处理器测试。
/// </summary>
public class StoreNotificationProcessorTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 11, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// Apple 退款在 P0 路由到整包退款命令。
    /// </summary>
    [Fact]
    public async Task ProcessAsync_Should_Route_Prorated_Refund_To_Full_Refund_Command()
    {
        var mediator = new Mock<IMediator>();
        var signedPayloadVerifier = new Mock<IAppleSignedPayloadVerifier>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var receiptId = new StoreNotificationReceiptId(Guid.NewGuid());
        var transactionId = new StoreTransactionId(Guid.NewGuid());
        mediator.Setup(x => x.Send(
                It.IsAny<GetStoreNotificationReceiptQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReceipt(receiptId: receiptId));
        mediator.Setup(x => x.Send(
                It.IsAny<ImportUnlinkedAppleStoreTransactionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionId);
        signedPayloadVerifier.Setup(x => x.VerifyTransactionPayloadAsync(
                "nested-jws",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSignedTransaction());
        var processor = new StoreNotificationProcessor(
            mediator: mediator.Object,
            appleSignedPayloadVerifier: signedPayloadVerifier.Object,
            googlePlayClient: googlePlayClient.Object,
            appleOptions: Microsoft.Extensions.Options.Options.Create(new AppleStoreOptions
            {
                BundleId = "com.lumavill.photorescue.dev",
                Environment = "Sandbox"
            }));

        await processor.ProcessAsync(receiptId: receiptId, cancellationToken: CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<ReverseStorePurchaseCommand>(command =>
                command.StoreTransactionId == transactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Google 全额作废通知必须查询 ProductPurchaseV2、记录 Fact，再调用退款命令。
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GoogleVoidedPurchase_Should_Query_Record_And_Reverse()
    {
        const string purchaseToken = "google-purchase-token";
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var receiptId = new StoreNotificationReceiptId(Guid.NewGuid());
        var transactionId = new StoreTransactionId(Guid.NewGuid());
        mediator.Setup(x => x.Send(
                It.IsAny<GetStoreNotificationReceiptQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGoogleReceipt(
                receiptId: receiptId,
                notificationType: "VOIDED_PURCHASE",
                platformNotification: $$"""
                "voidedPurchaseNotification": {
                  "purchaseToken": "{{purchaseToken}}",
                  "orderId": "GPA.1234-5678-9012-34567",
                  "productType": 2,
                  "refundType": 1
                }
                """));
        mediator.Setup(x => x.Send(
                It.IsAny<ImportUnlinkedGoogleStoreTransactionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionId);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(refundableQuantity: 0));
        var processor = CreateProcessor(
            mediator: mediator.Object,
            googlePlayClient: googlePlayClient.Object);

        await processor.ProcessAsync(receiptId: receiptId, cancellationToken: CancellationToken.None);

        googlePlayClient.Verify(x => x.GetPurchaseAsync(
            It.Is<GooglePlayPurchaseRequest>(request => request.PurchaseToken == purchaseToken),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Voided
                && command.AppliedStoreTransactionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<ReverseGoogleStorePurchaseCommand>(command =>
                command.StoreTransactionId == transactionId
                && command.RefundedAt == OccurredAt),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Voided
                && command.AppliedStoreTransactionId == transactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Google 延迟付款取消以 ProductPurchaseV2 的 Canceled 状态为准。
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GoogleCanceledPurchase_Should_Record_And_Cancel()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var receiptId = new StoreNotificationReceiptId(Guid.NewGuid());
        var transactionId = new StoreTransactionId(Guid.NewGuid());
        mediator.Setup(x => x.Send(
                It.IsAny<GetStoreNotificationReceiptQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGoogleReceipt(
                receiptId: receiptId,
                notificationType: "ONE_TIME_PRODUCT_CANCELED",
                platformNotification: """
                "oneTimeProductNotification": {
                  "notificationType": 2,
                  "purchaseToken": "google-purchase-token",
                  "sku": "com.lumavill.photorescue.dev.points20"
                }
                """));
        mediator.Setup(x => x.Send(
                It.IsAny<ImportUnlinkedGoogleStoreTransactionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionId);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(
                purchaseState: GooglePlayPurchaseState.Canceled,
                refundableQuantity: 0));
        var processor = CreateProcessor(
            mediator: mediator.Object,
            googlePlayClient: googlePlayClient.Object);

        await processor.ProcessAsync(receiptId: receiptId, cancellationToken: CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<CancelGoogleStorePurchaseCommand>(command =>
                command.StoreTransactionId == transactionId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Canceled
                && command.AppliedStoreTransactionId == transactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Google 购买通知导入交易、记录权威购买事实并调用购买对账命令。
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GooglePurchased_Should_Import_Record_And_Reconcile()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var receiptId = new StoreNotificationReceiptId(Guid.NewGuid());
        var transactionId = new StoreTransactionId(Guid.NewGuid());
        mediator.Setup(x => x.Send(
                It.IsAny<GetStoreNotificationReceiptQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGoogleReceipt(
                receiptId: receiptId,
                notificationType: "ONE_TIME_PRODUCT_PURCHASED",
                platformNotification: """
                "oneTimeProductNotification": {
                  "notificationType": 1,
                  "purchaseToken": "google-purchase-token",
                  "sku": "com.lumavill.photorescue.dev.points20"
                }
                """));
        mediator.Setup(x => x.Send(
                It.IsAny<ImportUnlinkedGoogleStoreTransactionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionId);
        mediator.Setup(x => x.Send(
                It.IsAny<ReconcileGoogleStorePurchaseCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconcileGoogleStorePurchaseResult(
                Outcome: ReconcileGoogleStorePurchaseOutcome.Completed,
                Status: StoreTransactionStatus.Verified,
                FailureCode: null));
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase());
        var processor = CreateProcessor(
            mediator: mediator.Object,
            googlePlayClient: googlePlayClient.Object);

        await processor.ProcessAsync(receiptId: receiptId, cancellationToken: CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<ImportUnlinkedGoogleStoreTransactionCommand>(command =>
                command.ExternalTransactionId == "google-purchase-token"),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Purchased
                && command.AppliedStoreTransactionId == transactionId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<ReconcileGoogleStorePurchaseCommand>(command =>
                command.StoreTransactionId == transactionId
                && command.PurchaseToken == "google-purchase-token"
                && command.Purchase.ProductId == "com.lumavill.photorescue.dev.points20"),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.IsAny<ReverseGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Google 按数量部分退款在 P0 进入不可重试失败，不能误做整笔冲正。
    /// </summary>
    [Fact]
    public async Task ProcessAsync_GooglePartialRefund_Should_DeadLetter_Without_Querying()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var receiptId = new StoreNotificationReceiptId(Guid.NewGuid());
        mediator.Setup(x => x.Send(
                It.IsAny<GetStoreNotificationReceiptQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGoogleReceipt(
                receiptId: receiptId,
                notificationType: "VOIDED_PURCHASE",
                platformNotification: """
                "voidedPurchaseNotification": {
                  "purchaseToken": "google-purchase-token",
                  "productType": 2,
                  "refundType": 2
                }
                """));
        var processor = CreateProcessor(
            mediator: mediator.Object,
            googlePlayClient: googlePlayClient.Object);

        var exception = await Assert.ThrowsAsync<StoreNotificationProcessingException>(() =>
            processor.ProcessAsync(receiptId: receiptId, cancellationToken: CancellationToken.None));

        Assert.Equal("GOOGLE_PARTIAL_REFUND_UNSUPPORTED", exception.Code);
        Assert.False(exception.IsRetryable);
        googlePlayClient.Verify(x => x.GetPurchaseAsync(
            It.IsAny<GooglePlayPurchaseRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GetStoreNotificationReceiptResult CreateReceipt(StoreNotificationReceiptId receiptId)
    {
        return new GetStoreNotificationReceiptResult(
            StoreNotificationReceiptId: receiptId,
            Store: AppStore.AppleAppStore,
            ExternalNotificationId: Guid.NewGuid().ToString("D"),
            NotificationType: "REFUND",
            NormalizedPayload: "{\"payload\":{\"data\":{\"signedTransactionInfo\":\"nested-jws\"}}}",
            PayloadHash: new byte[32],
            OccurredAt: OccurredAt,
            AttemptCount: 1);
    }

    private static AppleSignedPayload CreateSignedTransaction()
    {
        using var document = JsonDocument.Parse($$"""
        {
          "transactionId": "2000000123456789",
          "bundleId": "com.lumavill.photorescue.dev",
          "environment": "Sandbox",
          "signedDate": {{OccurredAt.ToUnixTimeMilliseconds()}},
          "revocationDate": {{OccurredAt.ToUnixTimeMilliseconds()}},
          "revocationType": "REFUND_PRORATED",
          "revocationPercentage": 85000
        }
        """);
        return new AppleSignedPayload(
            Payload: document.RootElement.Clone(),
            PayloadHash: "payload-hash",
            SourcePrincipal: "apple");
    }

    private static StoreNotificationProcessor CreateProcessor(
        IMediator mediator,
        IGooglePlayClient googlePlayClient)
    {
        return new StoreNotificationProcessor(
            mediator: mediator,
            appleSignedPayloadVerifier: Mock.Of<IAppleSignedPayloadVerifier>(),
            googlePlayClient: googlePlayClient,
            appleOptions: Microsoft.Extensions.Options.Options.Create(new AppleStoreOptions
            {
                BundleId = "com.lumavill.photorescue.dev",
                Environment = "Sandbox"
            }));
    }

    private static GetStoreNotificationReceiptResult CreateGoogleReceipt(
        StoreNotificationReceiptId receiptId,
        string notificationType,
        string platformNotification)
    {
        return new GetStoreNotificationReceiptResult(
            StoreNotificationReceiptId: receiptId,
            Store: AppStore.GooglePlay,
            ExternalNotificationId: "google-message-id",
            NotificationType: notificationType,
            NormalizedPayload: $$"""
            {
              "schemaVersion": 1,
              "parserVersion": 1,
              "messageId": "google-message-id",
              "notificationType": "{{notificationType}}",
              "payload": {
                "version": "1.0",
                "packageName": "com.lumavill.photorescue.dev",
                "eventTimeMillis": "{{OccurredAt.ToUnixTimeMilliseconds()}}",
                {{platformNotification}}
              }
            }
            """,
            PayloadHash: new byte[32],
            OccurredAt: OccurredAt,
            AttemptCount: 1);
    }

    private static GooglePlayPurchase CreateGooglePurchase(
        GooglePlayPurchaseState purchaseState = GooglePlayPurchaseState.Purchased,
        int refundableQuantity = 1)
    {
        return new GooglePlayPurchase(
            ProductId: "com.lumavill.photorescue.dev.points20",
            PurchaseState: purchaseState,
            IsConsumed: true,
            Quantity: 1,
            RefundableQuantity: refundableQuantity,
            ObfuscatedExternalAccountId: Guid.NewGuid().ToString("D"),
            OrderId: "GPA.1234-5678-9012-34567",
            PurchaseCompletedAt: OccurredAt.AddMinutes(-1),
            IsTestPurchase: true,
            IsAcknowledged: true,
            SnapshotHash: "google-snapshot-hash");
    }
}
