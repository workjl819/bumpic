using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetCorePal.Extensions.Primitives;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Application.Commands.StoreTransaction;
using Bumpic.Web.Application.Commands.StoreTransactionFact;
using Bumpic.Web.Application.Queries.StoreTransaction;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Application.Jobs;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 待验证商店交易每小时补偿任务测试。
/// </summary>
public class VerifyPendingStoreTransactionJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// Apple 有效购买交给现有对账命令完成认领和入账。
    /// </summary>
    [Fact]
    public async Task RunAsync_ApplePurchased_InvokesReconciliationCommand()
    {
        var mediator = new Mock<IMediator>();
        var appleClient = new Mock<IAppleAppStoreServerApiClient>();
        var candidate = CreateCandidate(AppStore.AppleAppStore, "2000000123456789");
        SetupCandidates(mediator, candidate);
        appleClient.Setup(x => x.GetTransactionInfoAsync(
                candidate.ExternalTransactionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApplePayload(revoked: false));
        mediator.Setup(x => x.Send(
                It.IsAny<ReconcileAppleStorePurchaseCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconcileAppleStorePurchaseResult(
                ReconcileAppleStorePurchaseOutcome.Completed,
                StoreTransactionStatus.Verified,
                null));
        var job = CreateJob(mediator.Object, appleClient.Object, Mock.Of<IGooglePlayClient>());

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<ReconcileAppleStorePurchaseCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId
                && command.Transaction.TransactionId == candidate.ExternalTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Apple 已退款交易记录事实并调用退款命令。
    /// </summary>
    [Fact]
    public async Task RunAsync_AppleRefunded_RecordsFactAndReverses()
    {
        var mediator = new Mock<IMediator>();
        var appleClient = new Mock<IAppleAppStoreServerApiClient>();
        var candidate = CreateCandidate(AppStore.AppleAppStore, "2000000123456789");
        SetupCandidates(mediator, candidate);
        appleClient.Setup(x => x.GetTransactionInfoAsync(
                candidate.ExternalTransactionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApplePayload(revoked: true));
        var job = CreateJob(mediator.Object, appleClient.Object, Mock.Of<IGooglePlayClient>());

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<ReverseStorePurchaseCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Refunded
                && command.AppliedStoreTransactionId == candidate.StoreTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Google 延迟付款保持 PendingVerification，等待下一小时继续补查。
    /// </summary>
    [Fact]
    public async Task RunAsync_GooglePending_DoesNotMutateTransaction()
    {
        var mediator = new Mock<IMediator>();
        var googleClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate(AppStore.GooglePlay, "purchase-token");
        SetupCandidates(mediator, candidate);
        googleClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(GooglePlayPurchaseState.Pending));
        var job = CreateJob(mediator.Object, Mock.Of<IAppleAppStoreServerApiClient>(), googleClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.IsAny<ReconcileGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(x => x.Send(
            It.IsAny<CancelGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(x => x.Send(
            It.IsAny<ReverseGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Google 延迟付款取消后记录事实并调用取消命令。
    /// </summary>
    [Fact]
    public async Task RunAsync_GoogleCanceled_RecordsFactAndCancels()
    {
        var mediator = new Mock<IMediator>();
        var googleClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate(AppStore.GooglePlay, "purchase-token");
        SetupCandidates(mediator, candidate);
        googleClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGooglePurchase(GooglePlayPurchaseState.Canceled));
        var job = CreateJob(mediator.Object, Mock.Of<IAppleAppStoreServerApiClient>(), googleClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<CancelGoogleStorePurchaseCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Canceled
                && command.AppliedStoreTransactionId == candidate.StoreTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 平台临时不可用时不调用任何状态变更命令。
    /// </summary>
    [Fact]
    public async Task RunAsync_StoreTemporarilyUnavailable_LeavesTransactionPending()
    {
        var mediator = new Mock<IMediator>();
        var googleClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate(AppStore.GooglePlay, "purchase-token");
        SetupCandidates(mediator, candidate);
        googleClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreClientException(
                "STORE_SERVICE_UNAVAILABLE",
                true,
                "temporary"));
        var job = CreateJob(mediator.Object, Mock.Of<IAppleAppStoreServerApiClient>(), googleClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.IsAny<ReconcileGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(x => x.Send(
            It.IsAny<CancelGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(x => x.Send(
            It.IsAny<ReverseGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static VerifyPendingStoreTransactionJob CreateJob(
        IMediator mediator,
        IAppleAppStoreServerApiClient appleClient,
        IGooglePlayClient googleClient)
    {
        return new VerifyPendingStoreTransactionJob(
            mediator,
            appleClient,
            googleClient,
            new FixedClock(Now),
            NullLogger<VerifyPendingStoreTransactionJob>.Instance);
    }

    private static void SetupCandidates(
        Mock<IMediator> mediator,
        PendingVerificationStoreTransactionResult candidate)
    {
        mediator.Setup(x => x.Send(
                It.IsAny<GetPendingVerificationStoreTransactionsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
    }

    private static PendingVerificationStoreTransactionResult CreateCandidate(
        AppStore store,
        string externalTransactionId)
    {
        return new PendingVerificationStoreTransactionResult(
            new StoreTransactionId(Guid.NewGuid()),
            store,
            externalTransactionId,
            Now.AddHours(-1));
    }

    private static AppleSignedPayload CreateApplePayload(bool revoked)
    {
        var revocation = revoked
            ? $",\"revocationDate\":{Now.AddMinutes(-1).ToUnixTimeMilliseconds()}"
            : string.Empty;
        using var document = JsonDocument.Parse($$"""
        {
          "transactionId": "2000000123456789",
          "productId": "com.lumavill.photorescue.dev.points20",
          "bundleId": "com.lumavill.photorescue.dev",
          "environment": "Sandbox",
          "type": "Consumable",
          "quantity": 1,
          "purchaseDate": {{Now.AddMinutes(-10).ToUnixTimeMilliseconds()}},
          "signedDate": {{Now.ToUnixTimeMilliseconds()}}{{revocation}}
        }
        """);
        return new AppleSignedPayload(document.RootElement.Clone(), "apple-payload-hash", "apple");
    }

    private static GooglePlayPurchase CreateGooglePurchase(GooglePlayPurchaseState state)
    {
        return new GooglePlayPurchase(
            ProductId: "com.lumavill.photorescue.dev.points20",
            PurchaseState: state,
            IsConsumed: false,
            Quantity: 1,
            RefundableQuantity: 1,
            ObfuscatedExternalAccountId: Guid.NewGuid().ToString("D"),
            OrderId: "GPA.1234-5678-9012-34567",
            PurchaseCompletedAt: state == GooglePlayPurchaseState.Purchased ? Now : null,
            IsTestPurchase: true,
            IsAcknowledged: true,
            SnapshotHash: "google-snapshot-hash");
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow.UtcDateTime;

        public DateTime Now { get; } = utcNow.UtcDateTime;
    }
}
