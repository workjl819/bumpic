namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 校验客户端 transactionJWS 并与 App Store Server API 权威交易比对的 Apple 验单客户端。
/// </summary>
public sealed class MimoAppleStoreClient(
    AppleSignedDataPayloadVerifier payloadVerifier,
    IAppleAppStoreServerApiClient appStoreServerApiClient)
    : IAppleStoreClient, IAppleSignedPayloadVerifier
{
    /// <inheritdoc />
    public async Task<AppleStoreTransaction> VerifyTransactionAsync(
        AppleStoreVerificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TransactionJws);

        // 1. 验证客户端 transactionJWS：证书链、签名和部署字段。
        var clientSignedPayload = await payloadVerifier.VerifyTransactionPayloadAsync(
            signedPayload: request.TransactionJws,
            cancellationToken: cancellationToken);
        var clientTransaction = AppleTransactionPayload.Read(payload: clientSignedPayload.Payload);
        EnsureMatchesRequest(request: request, transaction: clientTransaction);

        // 2. 通过 App Store Server API 查询权威签名交易信息。
        var authoritativeSignedPayload = await appStoreServerApiClient.GetTransactionInfoAsync(
            transactionId: clientTransaction.TransactionId,
            cancellationToken: cancellationToken);
        var authoritativeTransaction = AppleTransactionPayload.Read(
            payload: authoritativeSignedPayload.Payload);

        // 3. 客户端交易与服务端权威交易必须描述同一笔购买。
        clientTransaction.EnsureMatchesAuthoritativeTransaction(
            authoritative: authoritativeTransaction);
        return authoritativeTransaction.CreateSnapshot(
            payloadHash: authoritativeSignedPayload.PayloadHash);
    }

    /// <inheritdoc />
    public Task<AppleSignedPayload> VerifyNotificationPayloadAsync(
        string signedPayload,
        CancellationToken cancellationToken)
    {
        return payloadVerifier.VerifyNotificationPayloadAsync(
            signedPayload: signedPayload,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<AppleSignedPayload> VerifyTransactionPayloadAsync(
        string signedPayload,
        CancellationToken cancellationToken)
    {
        return payloadVerifier.VerifyTransactionPayloadAsync(
            signedPayload: signedPayload,
            cancellationToken: cancellationToken);
    }

    private static void EnsureMatchesRequest(
        AppleStoreVerificationRequest request,
        AppleTransactionPayload transaction)
    {
        if (!string.IsNullOrWhiteSpace(request.TransactionId)
            && !string.Equals(
                transaction.TransactionId,
                request.TransactionId.Trim(),
                StringComparison.Ordinal))
        {
            throw Invalid(message: "Apple 签名交易标识与请求不一致。");
        }
    }

    private static StoreClientException Invalid(string message)
    {
        return new StoreClientException(
            code: "PURCHASE_INVALID",
            isRetryable: false,
            message: message);
    }
}
