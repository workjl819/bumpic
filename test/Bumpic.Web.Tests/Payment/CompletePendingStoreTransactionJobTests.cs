using MediatR;
using Microsoft.Extensions.Logging;
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
/// Google Play 待消费交易补偿任务测试。
/// </summary>
public class CompletePendingStoreTransactionJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// 到期交易查询成功后将权威快照交给对账命令完成消费和入账。
    /// </summary>
    [Fact]
    public async Task RunAsync_DuePurchasedTransaction_InvokesReconciliationCommand()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate();
        SetupCandidates(mediator: mediator, candidate: candidate);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.Is<GooglePlayPurchaseRequest>(request => request.PurchaseToken == candidate.PurchaseToken),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePurchase());
        mediator.Setup(x => x.Send(
                It.IsAny<ReconcileGoogleStorePurchaseCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconcileGoogleStorePurchaseResult(
                Outcome: ReconcileGoogleStorePurchaseOutcome.Completed,
                Status: StoreTransactionStatus.Verified,
                FailureCode: null));
        var job = CreateJob(mediator: mediator.Object, googlePlayClient: googlePlayClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<ReconcileGoogleStorePurchaseCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId
                && command.PurchaseToken == candidate.PurchaseToken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 权威查询临时失败时记录失败次数和下一次补偿时间。
    /// </summary>
    [Fact]
    public async Task RunAsync_AuthoritativeQueryFails_SchedulesRetry()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate();
        SetupCandidates(mediator: mediator, candidate: candidate);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "temporary"));
        var job = CreateJob(mediator: mediator.Object, googlePlayClient: googlePlayClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<RecordGoogleStoreConsumptionFailureCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId
                && command.FailureCode == "STORE_SERVICE_UNAVAILABLE"
                && command.IsRetryable),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.IsAny<ReconcileGoogleStorePurchaseCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 权威查询返回永久错误时停止高频重试并记录为非临时失败。
    /// </summary>
    [Fact]
    public async Task RunAsync_AuthoritativeQueryPermanentlyFails_SchedulesLowFrequencyRecheck()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate();
        SetupCandidates(mediator: mediator, candidate: candidate);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreClientException(
                code: "PURCHASE_INVALID",
                isRetryable: false,
                message: "permanent"));
        var job = CreateJob(mediator: mediator.Object, googlePlayClient: googlePlayClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<RecordGoogleStoreConsumptionFailureCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId
                && command.FailureCode == "PURCHASE_INVALID"
                && !command.IsRetryable),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 等待消费超过二十四小时的交易会产生错误级别告警日志。
    /// </summary>
    [Fact]
    public async Task RunAsync_PendingForMoreThanTwentyFourHours_LogsAlert()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var logger = new Mock<ILogger<CompletePendingStoreTransactionJob>>();
        var candidate = CreateCandidate(createdAt: Now.AddHours(-25));
        SetupCandidates(mediator: mediator, candidate: candidate);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePurchase());
        mediator.Setup(x => x.Send(
                It.IsAny<ReconcileGoogleStorePurchaseCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconcileGoogleStorePurchaseResult(
                Outcome: ReconcileGoogleStorePurchaseOutcome.Completed,
                Status: StoreTransactionStatus.Verified,
                FailureCode: null));
        var job = CreateJob(
            mediator: mediator.Object,
            googlePlayClient: googlePlayClient.Object,
            logger: logger.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        logger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("超过 24 小时", StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    /// <summary>
    /// 待消费交易已被退款时记录对账事实并转为作废，不再尝试消费。
    /// </summary>
    [Fact]
    public async Task RunAsync_PurchaseWasRefunded_RecordsFactAndVoidsTransaction()
    {
        var mediator = new Mock<IMediator>();
        var googlePlayClient = new Mock<IGooglePlayClient>();
        var candidate = CreateCandidate();
        SetupCandidates(mediator: mediator, candidate: candidate);
        googlePlayClient.Setup(x => x.GetPurchaseAsync(
                It.IsAny<GooglePlayPurchaseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePurchase(refundableQuantity: 0));
        var job = CreateJob(mediator: mediator.Object, googlePlayClient: googlePlayClient.Object);

        await job.RunAsync(TestContext.Current.CancellationToken);

        mediator.Verify(x => x.Send(
            It.Is<ReverseGoogleStorePurchaseCommand>(command =>
                command.StoreTransactionId == candidate.StoreTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(x => x.Send(
            It.Is<RecordStoreTransactionFactCommand>(command =>
                command.Type == StoreTransactionFactType.Refunded
                && command.AppliedStoreTransactionId == candidate.StoreTransactionId),
            It.IsAny<CancellationToken>()), Times.Once);
        googlePlayClient.Verify(x => x.ConsumeAsync(
            It.IsAny<GooglePlayConsumptionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CompletePendingStoreTransactionJob CreateJob(
        IMediator mediator,
        IGooglePlayClient googlePlayClient,
        ILogger<CompletePendingStoreTransactionJob>? logger = null)
    {
        return new CompletePendingStoreTransactionJob(
            mediator: mediator,
            googlePlayClient: googlePlayClient,
            clock: new FixedClock(Now),
            logger: logger ?? NullLogger<CompletePendingStoreTransactionJob>.Instance);
    }

    private static void SetupCandidates(
        Mock<IMediator> mediator,
        PendingConsumptionStoreTransactionResult candidate)
    {
        mediator.Setup(x => x.Send(
                It.IsAny<GetDuePendingConsumptionStoreTransactionsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
    }

    private static PendingConsumptionStoreTransactionResult CreateCandidate(DateTimeOffset? createdAt = null)
    {
        return new PendingConsumptionStoreTransactionResult(
            StoreTransactionId: new StoreTransactionId(Guid.NewGuid()),
            PurchaseToken: "purchase-token",
            ProductId: "com.lumavill.photorescue.dev.points20",
            ConsumptionAttemptCount: 1,
            CreatedAt: createdAt ?? Now.AddMinutes(-5));
    }

    private static GooglePlayPurchase CreatePurchase(int refundableQuantity = 1)
    {
        return new GooglePlayPurchase(
            ProductId: "com.lumavill.photorescue.dev.points20",
            PurchaseState: GooglePlayPurchaseState.Purchased,
            IsConsumed: false,
            Quantity: 1,
            RefundableQuantity: refundableQuantity,
            ObfuscatedExternalAccountId: Guid.NewGuid().ToString("D"),
            OrderId: "GPA.1234-5678-9012-34567",
            PurchaseCompletedAt: Now.AddMinutes(-1),
            IsTestPurchase: true,
            IsAcknowledged: true,
            SnapshotHash: "snapshot-hash");
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow.UtcDateTime;

        public DateTime Now { get; } = utcNow.UtcDateTime;
    }
}
