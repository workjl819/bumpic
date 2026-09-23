using Microsoft.Extensions.Options;
using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Exceptions;
using Mimo.AppStoreServerLibrary.Models;
using Bumpic.Web.Options;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 基于 Mimo.AppStoreServerLibrary 的 App Store Server API 适配器。
/// </summary>
public sealed class MimoAppleAppStoreServerApiClient(
    IOptions<AppleStoreOptions> options,
    IHttpClientFactory httpClientFactory,
    AppleSignedDataPayloadVerifier payloadVerifier) : IAppleAppStoreServerApiClient
{
    private const string HttpClientName = "apple-app-store-server-api";
    private const string LocalTestingEnvironmentName = "LocalTesting";
    private readonly Lock _clientLock = new();
    private AppStoreServerApiClient? _client;

    /// <inheritdoc />
    public async Task<AppleSignedPayload> GetTransactionInfoAsync(
        string transactionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        var configuration = options.Value;
        TransactionInfoResponse? response;
        try
        {
            response = await ResolveClient(configuration: configuration)
                .GetTransactionInfo(transactionId: transactionId.Trim())
                .WaitAsync(cancellationToken);
        }
        catch (ApiException exception)
        {
            throw TranslateApiFailure(exception: exception);
        }
        catch (HttpRequestException exception)
        {
            throw StoreUnavailable(
                message: "Apple App Store Server API 暂时不可用。",
                innerException: exception);
        }
        catch (Exception exception) when (exception is OperationCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            throw StoreUnavailable(
                message: "Apple App Store Server API 调用超时。",
                innerException: exception);
        }

        var signedTransactionInfo = response?.SignedTransactionInfo;
        if (string.IsNullOrWhiteSpace(signedTransactionInfo))
        {
            throw new StoreClientException(
                code: "PURCHASE_INVALID",
                isRetryable: false,
                message: "Apple App Store Server API 未返回签名交易信息。");
        }

        return await payloadVerifier.VerifyTransactionPayloadAsync(
            signedPayload: signedTransactionInfo,
            cancellationToken: cancellationToken);
    }

    private AppStoreServerApiClient ResolveClient(AppleStoreOptions configuration)
    {
        if (_client is not null)
        {
            return _client;
        }

        lock (_clientLock)
        {
            // 凭据或密钥文件缺失时保持可重试失败，不缓存异常，配置补齐后无需重启即可恢复。
            return _client ??= CreateClient(configuration: configuration);
        }
    }

    private AppStoreServerApiClient CreateClient(AppleStoreOptions configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.BundleId))
        {
            throw StoreUnavailable(message: "Apple Bundle ID 未配置。");
        }

        var apiOptions = configuration.AppStoreServerApi;
        if (string.IsNullOrWhiteSpace(apiOptions.IssuerId) || string.IsNullOrWhiteSpace(apiOptions.KeyId))
        {
            throw StoreUnavailable(message: "Apple App Store Server API 凭据未配置。");
        }

        var environment = AppleStoreEnvironmentResolver.Resolve(
            environmentName: configuration.Environment);
        if (string.Equals(
                environment.Name,
                LocalTestingEnvironmentName,
                StringComparison.Ordinal))
        {
            throw StoreUnavailable(message: "Apple 本地测试环境不调用 App Store Server API。");
        }

        return new AppStoreServerApiClient(
            signingKey: ResolveSigningKey(apiOptions: apiOptions),
            keyId: apiOptions.KeyId.Trim(),
            issuerId: apiOptions.IssuerId.Trim(),
            bundleId: configuration.BundleId.Trim(),
            environment: environment,
            httpClient: httpClientFactory.CreateClient(name: HttpClientName));
    }

    /// <summary>
    /// 读取配置提供的 .p8 私钥内容；缺失或格式非法时保持可重试失败。
    /// </summary>
    private static string ResolveSigningKey(AppleStoreServerApiOptions apiOptions)
    {
        if (string.IsNullOrWhiteSpace(apiOptions.PrivateKeyPem))
        {
            throw StoreUnavailable(message: "Apple App Store Server API 私钥未配置。");
        }

        var signingKey = apiOptions.PrivateKeyPem.Trim();
        if (!signingKey.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal))
        {
            throw StoreUnavailable(message: "Apple App Store Server API 私钥不是合法的 .p8 PEM 内容。");
        }

        return signingKey;
    }

    private static StoreClientException TranslateApiFailure(ApiException exception)
    {
        return StoreUnavailable(
            message: $"Apple App Store Server API 调用失败，状态码 {exception.HttpStatusCode?.ToString() ?? "未知"}。",
            innerException: exception);
    }

    private static StoreClientException StoreUnavailable(
        string message,
        Exception? innerException = null)
    {
        return innerException is null
            ? new StoreClientException(code: "STORE_SERVICE_UNAVAILABLE", isRetryable: true, message: message)
            : new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: message,
                innerException: innerException);
    }
}
