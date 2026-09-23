using System.Security.Cryptography;
using System.Text.Json;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Play Developer API 服务创建测试。
/// </summary>
public class GooglePlayServiceFactoryTests
{
    /// <summary>
    /// 服务账号配置完整时创建指向配置根地址的服务。
    /// </summary>
    [Fact]
    public void Create_Should_Build_Service_With_Configured_Base_Address()
    {
        using var key = RSA.Create(2048);
        var factory = CreateFactory(
            packageName: "com.lumavill.photorescue",
            serviceAccountJson: CreateServiceAccountJson(key: key),
            apiBaseAddress: "http://localhost:5099/");

        var service = factory.Create();

        Assert.Equal("http://localhost:5099/", service.BaseUri);
        Assert.False(string.IsNullOrWhiteSpace(service.Name));
    }

    /// <summary>
    /// 历史配置带 API 版本路径时，官方客户端仍使用服务根地址。
    /// </summary>
    [Fact]
    public void Create_Should_Normalize_Legacy_Base_Address()
    {
        using var key = RSA.Create(2048);
        var factory = CreateFactory(
            packageName: "com.lumavill.photorescue",
            serviceAccountJson: CreateServiceAccountJson(key: key),
            apiBaseAddress: "https://androidpublisher.googleapis.com/androidpublisher/v3/");

        var service = factory.Create();

        Assert.Equal("https://androidpublisher.googleapis.com/", service.BaseUri);
    }

    /// <summary>
    /// 缺少包名时按可重试故障处理。
    /// </summary>
    [Fact]
    public void Create_Should_Fail_Closed_Without_Package_Name()
    {
        using var key = RSA.Create(2048);
        var factory = CreateFactory(
            packageName: string.Empty,
            serviceAccountJson: CreateServiceAccountJson(key: key));

        var exception = Assert.Throws<StoreClientException>(() => factory.Create());

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 缺少服务账号 JSON 密钥时按可重试故障处理。
    /// </summary>
    [Fact]
    public void Create_Should_Fail_Closed_Without_Credentials()
    {
        var factory = CreateFactory(
            packageName: "com.lumavill.photorescue",
            serviceAccountJson: string.Empty);

        var exception = Assert.Throws<StoreClientException>(() => factory.Create());

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 服务账号 JSON 密钥非法时按可重试故障处理。
    /// </summary>
    [Fact]
    public void Create_Should_Fail_Closed_For_Invalid_Service_Account_Json()
    {
        var factory = CreateFactory(
            packageName: "com.lumavill.photorescue",
            serviceAccountJson: "not-a-json-key");

        var exception = Assert.Throws<StoreClientException>(() => factory.Create());

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    /// <summary>
    /// 非法 API 根地址在创建服务时直接失败。
    /// </summary>
    [Fact]
    public void Create_Should_Fail_Closed_For_Invalid_Base_Address()
    {
        using var key = RSA.Create(2048);
        var factory = CreateFactory(
            packageName: "com.lumavill.photorescue",
            serviceAccountJson: CreateServiceAccountJson(key: key),
            apiBaseAddress: "not-an-absolute-uri");

        var exception = Assert.Throws<StoreClientException>(() => factory.Create());

        Assert.Equal("STORE_SERVICE_UNAVAILABLE", exception.Code);
        Assert.True(exception.IsRetryable);
    }

    private static GooglePlayServiceFactory CreateFactory(
        string packageName,
        string serviceAccountJson,
        string apiBaseAddress = "https://androidpublisher.googleapis.com/")
    {
        return new GooglePlayServiceFactory(
            Microsoft.Extensions.Options.Options.Create(new GooglePlayOptions
            {
                PackageName = packageName,
                ServiceAccountJson = serviceAccountJson,
                ApiBaseAddress = apiBaseAddress
            }));
    }

    /// <summary>
    /// 生成测试用服务账号 JSON 密钥内容。
    /// </summary>
    private static string CreateServiceAccountJson(RSA key)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = "service_account",
            ["project_id"] = "photorescue-test",
            ["private_key_id"] = "key-id",
            ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "play-service@example.iam.gserviceaccount.com",
            ["client_id"] = "1234567890",
            ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
            ["auth_provider_x509_cert_url"] = "https://www.googleapis.com/oauth2/v1/certs",
            ["client_x509_cert_url"] = "https://www.googleapis.com/robot/v1/metadata/x509/play-service",
            ["universe_domain"] = "googleapis.com"
        });
    }
}
