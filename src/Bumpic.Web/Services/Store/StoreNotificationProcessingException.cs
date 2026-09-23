namespace Bumpic.Web.Services.Store;

/// <summary>
/// 可分类重试语义的商店通知处理异常。
/// </summary>
public class StoreNotificationProcessingException : Exception
{
    public StoreNotificationProcessingException(string code, bool isRetryable, string message)
        : base(message)
    {
        Code = code;
        IsRetryable = isRetryable;
    }

    /// <summary>
    /// 稳定失败码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 是否允许本地退避重试。
    /// </summary>
    public bool IsRetryable { get; }
}
