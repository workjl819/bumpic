namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Apple App Store Server API 端口。
/// </summary>
public interface IAppleAppStoreServerApiClient
{
    /// <summary>
    /// 查询指定交易号的权威签名交易信息。
    /// </summary>
    Task<AppleSignedPayload> GetTransactionInfoAsync(
        string transactionId,
        CancellationToken cancellationToken);
}
