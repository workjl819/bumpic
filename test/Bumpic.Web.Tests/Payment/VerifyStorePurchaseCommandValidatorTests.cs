using FluentValidation.TestHelper;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StorePurchase;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 商店验单命令验证器测试。
/// </summary>
public class VerifyStorePurchaseCommandValidatorTests
{
    private readonly VerifyStorePurchaseCommandValidator _validator = new();

    /// <summary>
    /// Google 可以附带客户端展示的订单号，服务端后续与权威订单号比对。
    /// </summary>
    [Fact]
    public void Validate_GoogleWithOptionalOrderId_HasNoErrors()
    {
        var result = _validator.TestValidate(CreateGoogleCommand() with
        {
            TransactionId = "GPA.3375-9264-2560-82448"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static VerifyStorePurchaseCommand CreateGoogleCommand()
    {
        return new VerifyStorePurchaseCommand(
            UserAccountId: new UserAccountId(Guid.NewGuid()),
            Store: AppStore.GooglePlay,
            ProductId: "com.lumavill.photorescue.dev.points60",
            TransactionId: null,
            TransactionJws: null,
            PurchaseToken: "purchase-token",
            ExternalAccountToken: Guid.NewGuid().ToString("D"),
            IdempotencyKey: "idempotency-key");
    }
}
