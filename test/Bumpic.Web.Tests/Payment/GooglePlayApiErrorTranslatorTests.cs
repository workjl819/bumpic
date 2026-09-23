using System.Net;
using Google;
using Bumpic.Web.Clients.Store;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google API 异常到商店失败语义的翻译测试。
/// </summary>
public class GooglePlayApiErrorTranslatorTests
{
    /// <summary>
    /// 权威 400 结果确认购买令牌无效，属于确定性失败。
    /// </summary>
    [Fact]
    public void PurchaseQuery_Should_Be_Deterministic_For_BadRequest()
    {
        var exception = GooglePlayApiErrorTranslator.TranslatePurchaseQueryFailure(
            CreateApiException(statusCode: HttpStatusCode.BadRequest));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.False(exception.IsRetryable);
    }

    /// <summary>
    /// 查询不到购买或服务端错误按可重试失败处理。
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void PurchaseQuery_Should_Be_Retryable_For_Temporary_Failures(HttpStatusCode statusCode)
    {
        var exception = GooglePlayApiErrorTranslator.TranslatePurchaseQueryFailure(
            CreateApiException(statusCode: statusCode));

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 并发消费冲突允许后台补偿任务重试。
    /// </summary>
    [Fact]
    public void Consumption_Should_Be_Retryable_For_Conflict()
    {
        var exception = GooglePlayApiErrorTranslator.TranslateConsumptionFailure(
            CreateApiException(statusCode: HttpStatusCode.Conflict));

        Assert.Equal("PURCHASE_CONSUMPTION_PENDING", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 凭据问题按可重试失败处理，不把用户购买判为无效。
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void PurchaseQuery_Should_Be_Retryable_For_Credential_Failures(HttpStatusCode statusCode)
    {
        var exception = GooglePlayApiErrorTranslator.TranslatePurchaseQueryFailure(
            CreateApiException(statusCode: statusCode));

        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 消费失败保持待消费状态；服务端错误可重试，客户端错误不可重试。
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public void Consumption_Should_Keep_Pending_State(HttpStatusCode statusCode, bool expectedRetryable)
    {
        var exception = GooglePlayApiErrorTranslator.TranslateConsumptionFailure(
            CreateApiException(statusCode: statusCode));

        Assert.Equal("PURCHASE_CONSUMPTION_PENDING", exception.Code);
        Assert.Equal(expectedRetryable, exception.IsRetryable);
    }

    /// <summary>
    /// 网络故障翻译为可重试的商店不可用。
    /// </summary>
    [Fact]
    public void Transport_Should_Be_Retryable()
    {
        var exception = GooglePlayApiErrorTranslator.TranslateTransportFailure(
            new HttpRequestException("network"));

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    private static GoogleApiException CreateApiException(HttpStatusCode statusCode)
    {
        return new GoogleApiException(serviceName: "androidpublisher", message: "failed")
        {
            HttpStatusCode = statusCode
        };
    }
}
