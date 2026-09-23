using System.Text;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple 签名交易验单客户端测试。
/// </summary>
public class MimoAppleStoreClientTests
{
    private const string BundleId = TestAppleTransactionSigner.DefaultBundleId;
    private const string ProductId = TestAppleTransactionSigner.DefaultProductId;
    private const string TransactionId = TestAppleTransactionSigner.DefaultTransactionId;

    /// <summary>
    /// 客户端 JWS 与服务端权威交易一致时返回服务端快照。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Return_Authoritative_Transaction()
    {
        var appAccountToken = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now);
        var serverJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now.AddSeconds(1));
        var apiClient = new StubAppleAppStoreServerApiClient(
            options: options,
            signedTransactionInfo: serverJws);

        var transaction = await CreateClient(options, apiClient).VerifyTransactionAsync(
            new AppleStoreVerificationRequest(
                TransactionId: TransactionId,
                TransactionJws: clientJws),
            CancellationToken.None);

        Assert.Equal(TransactionId, transaction.TransactionId);
        Assert.Equal(ProductId, transaction.ProductId);
        Assert.Equal(appAccountToken, transaction.AppAccountToken);
        Assert.Equal("Sandbox", transaction.Environment);
        Assert.Equal(1, transaction.Quantity);
        Assert.Equal(4.99m, transaction.Amount);
        Assert.Equal("USD", transaction.CurrencyCode);
        Assert.False(transaction.IsRevoked);
        Assert.Equal(
            now.AddSeconds(1).ToUnixTimeMilliseconds(),
            transaction.SignedDate.ToUnixTimeMilliseconds());
        Assert.False(string.IsNullOrWhiteSpace(transaction.PayloadHash));
        Assert.Equal([TransactionId], apiClient.RequestedTransactionIds);
    }

    /// <summary>
    /// 客户端 JWS 验签失败时不查询服务端交易信息。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Reject_Tampered_Jws_Without_Server_Call()
    {
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            signedDate: now);
        var segments = clientJws.Split('.');
        var tamperedPayload = TestAppleTransactionSigner.EncodeBase64Url(
            Encoding.UTF8.GetBytes($"{{\"transactionId\":\"{TransactionId}\",\"bundleId\":\"{BundleId}\"}}"));
        var apiClient = new StubAppleAppStoreServerApiClient(options: options);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: $"{segments[0]}.{tamperedPayload}.{segments[2]}"),
                CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.False(exception.IsRetryable);
        Assert.Empty(apiClient.RequestedTransactionIds);
    }

    /// <summary>
    /// 服务端权威商品与客户端不一致时拒绝验单。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Reject_When_Server_Product_Differs()
    {
        var appAccountToken = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now);
        var serverJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: "com.lumavill.photorescue.dev.credits500",
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now);
        var apiClient = new StubAppleAppStoreServerApiClient(
            options: options,
            signedTransactionInfo: serverJws);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: clientJws),
                CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.Contains("商品", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 服务端权威账户绑定标识与客户端不一致时拒绝验单。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Reject_When_Server_Account_Token_Differs()
    {
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            signedDate: now);
        var serverJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            signedDate: now);
        var apiClient = new StubAppleAppStoreServerApiClient(
            options: options,
            signedTransactionInfo: serverJws);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: clientJws),
                CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.Contains("账户绑定标识", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 退款或撤销状态以服务端权威结果为准。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Use_Server_Revocation_State()
    {
        var appAccountToken = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now);
        var serverJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now.AddSeconds(1),
            revocationDate: now.AddSeconds(1).ToUnixTimeMilliseconds());
        var apiClient = new StubAppleAppStoreServerApiClient(
            options: options,
            signedTransactionInfo: serverJws);

        var transaction = await CreateClient(options, apiClient).VerifyTransactionAsync(
            new AppleStoreVerificationRequest(
                TransactionId: TransactionId,
                TransactionJws: clientJws),
            CancellationToken.None);

        Assert.True(transaction.IsRevoked);
        Assert.NotNull(transaction.RevocationDate);
    }

    /// <summary>
    /// 服务端查询的可重试失败原样向上传递，交给幂等层退避重试。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Propagate_Retryable_Server_Failure()
    {
        var appAccountToken = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: appAccountToken,
            signedDate: now);
        var apiClient = new StubAppleAppStoreServerApiClient(
            options: options,
            failure: new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "timeout"));

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: clientJws),
                CancellationToken.None));

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// Bundle ID 不属于当前部署时拒绝验单。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Reject_Other_BundleId()
    {
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: "com.other.application",
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            signedDate: now);
        var apiClient = new StubAppleAppStoreServerApiClient(options: options);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: clientJws),
                CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.Empty(apiClient.RequestedTransactionIds);
    }

    /// <summary>
    /// Apple 环境与当前部署不一致时拒绝验单。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Reject_Other_Environment()
    {
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var clientJws = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            environment: "Production",
            signedDate: now);
        var apiClient = new StubAppleAppStoreServerApiClient(options: options);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: clientJws),
                CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
    }

    /// <summary>
    /// 缺少根证书时按可重试故障处理，不把用户购买判为无效。
    /// </summary>
    [Fact]
    public async Task VerifyTransactionAsync_Should_Fail_Closed_Without_Root_Certificates()
    {
        var signed = TestAppleTransactionSigner.Sign(
            bundleId: BundleId,
            productId: ProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            environment: "Sandbox");
        var options = new AppleStoreOptions
        {
            BundleId = BundleId,
            Environment = "Sandbox"
        };
        var apiClient = new StubAppleAppStoreServerApiClient(options: options);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            CreateClient(options, apiClient).VerifyTransactionAsync(
                new AppleStoreVerificationRequest(
                    TransactionId: TransactionId,
                    TransactionJws: signed.Jws),
                CancellationToken.None));

        Assert.True(exception.IsRetryable);
    }

    private static MimoAppleStoreClient CreateClient(
        AppleStoreOptions options,
        IAppleAppStoreServerApiClient apiClient)
    {
        return new MimoAppleStoreClient(
            payloadVerifier: new AppleSignedDataPayloadVerifier(
                Microsoft.Extensions.Options.Options.Create(options)),
            appStoreServerApiClient: apiClient);
    }
}
