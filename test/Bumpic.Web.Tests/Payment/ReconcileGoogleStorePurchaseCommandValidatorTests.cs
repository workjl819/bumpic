using FluentValidation.TestHelper;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Clients.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google Play 通知购买对账命令验证器测试。
/// </summary>
public class ReconcileGoogleStorePurchaseCommandValidatorTests
{
    private readonly ReconcileGoogleStorePurchaseCommandValidator _validator = new();

    /// <summary>
    /// 已购买且可完整兑现的单数量快照通过验证。
    /// </summary>
    [Fact]
    public void Validate_CompletedSingleQuantityPurchase_HasNoErrors()
    {
        var result = _validator.TestValidate(CreateCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// 尚未完成或已发生退款的快照不能进入购买入账命令。
    /// </summary>
    [Theory]
    [InlineData(GooglePlayPurchaseState.Pending, 1)]
    [InlineData(GooglePlayPurchaseState.Canceled, 1)]
    [InlineData(GooglePlayPurchaseState.Purchased, 0)]
    public void Validate_NonGrantableSnapshot_HasErrors(
        GooglePlayPurchaseState purchaseState,
        int refundableQuantity)
    {
        var command = CreateCommand() with
        {
            Purchase = CreateCommand().Purchase with
            {
                PurchaseState = purchaseState,
                RefundableQuantity = refundableQuantity
            }
        };

        var result = _validator.TestValidate(command);

        Assert.False(result.IsValid);
    }

    private static ReconcileGoogleStorePurchaseCommand CreateCommand()
    {
        return new ReconcileGoogleStorePurchaseCommand(
            StoreTransactionId: new StoreTransactionId(Guid.NewGuid()),
            PurchaseToken: "purchase-token",
            Purchase: new GooglePlayPurchase(
                ProductId: "com.lumavill.photorescue.dev.points20",
                PurchaseState: GooglePlayPurchaseState.Purchased,
                IsConsumed: false,
                Quantity: 1,
                RefundableQuantity: 1,
                ObfuscatedExternalAccountId: Guid.NewGuid().ToString("D"),
                OrderId: "GPA.1234-5678-9012-34567",
                PurchaseCompletedAt: new DateTimeOffset(2026, 9, 15, 1, 2, 3, TimeSpan.Zero),
                IsTestPurchase: true,
                IsAcknowledged: true,
                SnapshotHash: "snapshot-hash"));
    }
}
