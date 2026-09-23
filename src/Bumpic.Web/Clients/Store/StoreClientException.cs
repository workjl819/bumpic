namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 商店客户端调用失败异常，区分确定性失败和可重试失败。
/// </summary>
public class StoreClientException : Exception
{
    /// <summary>
    /// 创建商店客户端调用失败异常。
    /// </summary>
    public StoreClientException(string code, bool isRetryable, string message)
        : base(message)
    {
        Code = code;
        IsRetryable = isRetryable;
    }

    /// <summary>
    /// 创建携带内部异常的商店客户端调用失败异常。
    /// </summary>
    public StoreClientException(string code, bool isRetryable, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        IsRetryable = isRetryable;
    }

    /// <summary>
    /// 稳定失败码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 是否属于商店超时、限流或临时故障。
    /// </summary>
    public bool IsRetryable { get; }
}
