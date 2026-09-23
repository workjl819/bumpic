namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Apple App Store 服务端验单端口。
/// </summary>
public interface IAppleStoreClient
{
    /// <summary>
    /// 验证客户端提交的 Apple 签名交易并返回权威交易快照。
    /// </summary>
    Task<AppleStoreTransaction> VerifyTransactionAsync(
        AppleStoreVerificationRequest request,
        CancellationToken cancellationToken);
}
