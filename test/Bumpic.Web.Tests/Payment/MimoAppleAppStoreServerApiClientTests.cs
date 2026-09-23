using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// App Store Server API 适配器测试。
/// </summary>
public class MimoAppleAppStoreServerApiClientTests
{
    private const string TransactionId = TestAppleTransactionSigner.DefaultTransactionId;

    /// <summary>
    /// 查询交易信息并返回已完成验签的权威交易载荷。
    /// </summary>
    [Fact]
    public async Task GetTransactionInfoAsync_Should_Return_Verified_Authoritative_Transaction()
    {
        var now = DateTimeOffset.UtcNow;
        using var chain = TestAppleCertificateChain.Create(now: now);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var signedTransactionInfo = TestAppleTransactionSigner.SignWithChain(
            chain: chain,
            bundleId: options.BundleId,
            productId: TestAppleTransactionSigner.DefaultProductId,
            transactionId: TransactionId,
            appAccountToken: Guid.NewGuid().ToString("D"),
            signedDate: now);
        var handler = new StubHttpMessageHandler(_ => CreateJsonResponse(
            statusCode: HttpStatusCode.OK,
            payload: $"{{\"signedTransactionInfo\":\"{signedTransactionInfo}\"}}"));
        var client = CreateClient(
            options: options,
            handler: handler,
            privateKeyPem: CreatePrivateKeyPem());

        var payload = await client.GetTransactionInfoAsync(
            transactionId: TransactionId,
            cancellationToken: CancellationToken.None);

        Assert.Equal(
            TransactionId,
            payload.Payload.GetProperty("transactionId").GetString());
        var request = Assert.Single(handler.Requests);
        Assert.Contains(
            $"/inApps/v1/transactions/{TransactionId}",
            request.RequestUri!.AbsoluteUri,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 缺少私钥内容时按可重试故障处理。
    /// </summary>
    [Fact]
    public async Task GetTransactionInfoAsync_Should_Be_Retryable_Without_Private_Key()
    {
        using var chain = TestAppleCertificateChain.Create(now: DateTimeOffset.UtcNow);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var client = CreateClient(
            options: options,
            handler: new StubHttpMessageHandler(_ => CreateJsonResponse(HttpStatusCode.OK, "{}")),
            privateKeyPem: string.Empty);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            client.GetTransactionInfoAsync(transactionId: TransactionId, CancellationToken.None));

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 缺少 Issuer 或 Key 标识时按可重试故障处理。
    /// </summary>
    [Fact]
    public async Task GetTransactionInfoAsync_Should_Be_Retryable_Without_Credentials()
    {
        using var chain = TestAppleCertificateChain.Create(now: DateTimeOffset.UtcNow);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var client = CreateClient(
            options: options,
            handler: new StubHttpMessageHandler(_ => CreateJsonResponse(HttpStatusCode.OK, "{}")),
            privateKeyPem: CreatePrivateKeyPem(),
            issuerId: string.Empty,
            keyId: string.Empty);

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            client.GetTransactionInfoAsync(transactionId: TransactionId, CancellationToken.None));

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// Apple 返回错误状态时按可重试故障处理，不把用户购买判为无效。
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetTransactionInfoAsync_Should_Be_Retryable_When_Api_Fails(HttpStatusCode statusCode)
    {
        using var chain = TestAppleCertificateChain.Create(now: DateTimeOffset.UtcNow);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var client = CreateClient(
            options: options,
            handler: new StubHttpMessageHandler(_ => CreateJsonResponse(
                statusCode: statusCode,
                payload: "{\"errorCode\":4040002,\"errorMessage\":\"failed\"}")),
            privateKeyPem: CreatePrivateKeyPem());

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            client.GetTransactionInfoAsync(transactionId: TransactionId, CancellationToken.None));

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 响应缺少签名交易信息时视为无效结果。
    /// </summary>
    [Fact]
    public async Task GetTransactionInfoAsync_Should_Reject_Response_Without_Signed_Transaction()
    {
        using var chain = TestAppleCertificateChain.Create(now: DateTimeOffset.UtcNow);
        var options = TestAppleTransactionSigner.CreateOptions(chain: chain);
        var client = CreateClient(
            options: options,
            handler: new StubHttpMessageHandler(_ => CreateJsonResponse(HttpStatusCode.OK, "{}")),
            privateKeyPem: CreatePrivateKeyPem());

        var exception = await Assert.ThrowsAsync<StoreClientException>(() =>
            client.GetTransactionInfoAsync(transactionId: TransactionId, CancellationToken.None));

        Assert.Equal("PURCHASE_INVALID", exception.Code);
        Assert.False(exception.IsRetryable);
    }

    private static MimoAppleAppStoreServerApiClient CreateClient(
        AppleStoreOptions options,
        HttpMessageHandler handler,
        string privateKeyPem,
        string issuerId = "69a6de70-0000-0000-0000-000000000000",
        string keyId = "2X9R4HXF34")
    {
        options.AppStoreServerApi.IssuerId = issuerId;
        options.AppStoreServerApi.KeyId = keyId;
        options.AppStoreServerApi.PrivateKeyPem = privateKeyPem;
        var wrappedOptions = Microsoft.Extensions.Options.Options.Create(options);
        return new MimoAppleAppStoreServerApiClient(
            options: wrappedOptions,
            httpClientFactory: new StubHttpClientFactory(handler: handler),
            payloadVerifier: new AppleSignedDataPayloadVerifier(wrappedOptions));
    }

    private static string CreatePrivateKeyPem()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportPkcs8PrivateKeyPem();
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.storekit-sandbox.itunes.apple.com/")
            };
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
