namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Apple JWS 通用签名载荷验证器。
/// </summary>
public interface IAppleSignedPayloadVerifier
{
    /// <summary>
    /// 校验 Apple 服务端通知外层 JWS 并返回规范化后的通知载荷。
    /// </summary>
    Task<AppleSignedPayload> VerifyNotificationPayloadAsync(
        string signedPayload,
        CancellationToken cancellationToken);

    /// <summary>
    /// 校验 Apple 签名交易 JWS 并返回规范化后的交易载荷。
    /// </summary>
    Task<AppleSignedPayload> VerifyTransactionPayloadAsync(
        string signedPayload,
        CancellationToken cancellationToken);
}
