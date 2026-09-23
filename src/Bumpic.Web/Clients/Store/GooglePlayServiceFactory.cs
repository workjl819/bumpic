using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Bumpic.Web.Options;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 使用官方 Google API 客户端创建 Play Developer API 服务。
/// </summary>
public sealed class GooglePlayServiceFactory(IOptions<GooglePlayOptions> options)
{
    private const string ApplicationName = "Bumpic";

    /// <summary>
    /// 创建已绑定服务账号凭据的 Android Publisher 服务。
    /// </summary>
    public AndroidPublisherService Create()
    {
        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.PackageName))
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Google Play 包名未配置。");
        }

        return new AndroidPublisherService(new Google.Apis.Services.BaseClientService.Initializer
        {
            HttpClientInitializer = CreateCredential(configuration: configuration),
            HttpClientTimeout = TimeSpan.FromSeconds(
                Math.Clamp(configuration.TimeoutSeconds, 1, 120)),
            BaseUri = NormalizeBaseAddress(configuration: configuration),
            ApplicationName = ApplicationName
        });
    }

    private static GoogleCredential CreateCredential(GooglePlayOptions configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ServiceAccountJson))
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Google Play 服务账号 JSON 密钥未配置。");
        }

        try
        {
            return GoogleCredential
                .FromJson(json: configuration.ServiceAccountJson)
                .CreateScoped(scopes: AndroidPublisherService.Scope.Androidpublisher);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                          or ArgumentException
                                          or System.Text.Json.JsonException)
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Google Play 服务账号 JSON 密钥不合法。",
                innerException: exception);
        }
    }

    private static string NormalizeBaseAddress(GooglePlayOptions configuration)
    {
        const string defaultBaseAddress = "https://androidpublisher.googleapis.com/";
        if (string.IsNullOrWhiteSpace(configuration.ApiBaseAddress))
        {
            return defaultBaseAddress;
        }

        if (!Uri.TryCreate(configuration.ApiBaseAddress.Trim(), UriKind.Absolute, out var configuredUri)
            || (!string.Equals(configuredUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(configuredUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Google Play API 根地址配置不合法。");
        }

        // 官方客户端自行拼接 androidpublisher/v3 路径，配置只保留服务根地址。
        return new UriBuilder(configuredUri)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri.AbsoluteUri;
    }
}
