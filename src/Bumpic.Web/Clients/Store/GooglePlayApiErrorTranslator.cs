using System.Net;
using Google;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 将 Google API 客户端异常翻译为稳定的商店失败语义。
/// </summary>
public static class GooglePlayApiErrorTranslator
{
    /// <summary>
    /// 翻译购买查询失败：只有权威的无效结果才是确定性失败。
    /// </summary>
    public static StoreClientException TranslatePurchaseQueryFailure(Exception exception)
    {
        if (exception is GoogleApiException { HttpStatusCode: HttpStatusCode.BadRequest })
        {
            return new StoreClientException(
                code: "PURCHASE_INVALID",
                isRetryable: false,
                message: "Google Play 权威结果确认购买令牌无效。",
                innerException: exception);
        }

        return new StoreClientException(
            code: "STORE_SERVICE_UNAVAILABLE",
            isRetryable: true,
            message: DescribeFailure(exception: exception, operation: "Google Play 购买查询"),
            innerException: exception);
    }

    /// <summary>
    /// 翻译消费失败：保持待消费状态，只有临时故障才允许重试。
    /// </summary>
    public static StoreClientException TranslateConsumptionFailure(GoogleApiException exception)
    {
        return new StoreClientException(
            code: "PURCHASE_CONSUMPTION_PENDING",
            isRetryable: IsRetryable(statusCode: exception.HttpStatusCode),
            message: DescribeFailure(exception: exception, operation: "Google Play 消费"),
            innerException: exception);
    }

    /// <summary>
    /// 翻译网络或超时故障。
    /// </summary>
    public static StoreClientException TranslateTransportFailure(Exception exception)
    {
        return new StoreClientException(
            code: "STORE_SERVICE_UNAVAILABLE",
            isRetryable: true,
            message: "Google Play 服务暂时不可用。",
            innerException: exception);
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 500
               || statusCode is HttpStatusCode.TooManyRequests
                   or HttpStatusCode.RequestTimeout
                   or HttpStatusCode.Conflict
                   or HttpStatusCode.Unauthorized
                   or HttpStatusCode.Forbidden;
    }

    private static string DescribeFailure(Exception exception, string operation)
    {
        return exception is GoogleApiException googleApiException
            ? $"{operation}失败，状态码 {(int)googleApiException.HttpStatusCode}。"
            : $"{operation}失败。";
    }
}
