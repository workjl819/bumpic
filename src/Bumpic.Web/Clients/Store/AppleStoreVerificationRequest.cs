namespace Bumpic.Web.Clients.Store;

/// <summary>
/// Apple 交易验单请求。
/// </summary>
public record AppleStoreVerificationRequest(
    string TransactionId,
    string TransactionJws);
