using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 测试用 App Store Server API 替身。
/// </summary>
internal sealed class StubAppleAppStoreServerApiClient : IAppleAppStoreServerApiClient
{
    private readonly AppleStoreOptions _options;
    private readonly string? _signedTransactionInfo;
    private readonly StoreClientException? _failure;

    internal StubAppleAppStoreServerApiClient(
        AppleStoreOptions options,
        string? signedTransactionInfo = null,
        StoreClientException? failure = null)
    {
        _options = options;
        _signedTransactionInfo = signedTransactionInfo;
        _failure = failure;
    }

    /// <summary>
    /// 记录被查询的交易号，用于断言未触发商店调用。
    /// </summary>
    internal List<string> RequestedTransactionIds { get; } = [];

    /// <inheritdoc />
    public async Task<AppleSignedPayload> GetTransactionInfoAsync(
        string transactionId,
        CancellationToken cancellationToken)
    {
        RequestedTransactionIds.Add(transactionId);
        if (_failure is not null)
        {
            throw _failure;
        }

        var verifier = new AppleSignedDataPayloadVerifier(
            Microsoft.Extensions.Options.Options.Create(_options));
        return await verifier.VerifyTransactionPayloadAsync(
            signedPayload: _signedTransactionInfo!,
            cancellationToken: cancellationToken);
    }
}
