using System.Globalization;
using System.Net;
using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Exceptions;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple App Store Server API 真实配置连通性测试。
/// </summary>
[Trait("Category", "PaymentLive")]
public class AppleAppStoreServerApiLiveTests(ITestOutputHelper output)
{
    private const string LiveTestEnvironmentVariable = "PAYMENT_LIVE_TESTS";
    private const string MissingTransactionId = "2999999999999999";

    /// <summary>
    /// 使用开发环境支付配置查询不存在的交易，Apple 返回交易不存在即证明鉴权和路由已调通。
    /// </summary>
    [Fact]
    public async Task GetTransactionInfo_WithConfiguredClient_ReturnsTransactionNotFound()
    {
        Assert.SkipUnless(
            string.Equals(
                Environment.GetEnvironmentVariable(LiveTestEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase),
            $"默认不访问真实 Apple 平台；请设置 {LiveTestEnvironmentVariable}=true 后单独运行。");

        var options = LoadDevelopmentConfiguration()
            .GetSection("Payment:Apple")
            .Get<AppleStoreOptions>();
        Assert.NotNull(options);
        Assert.False(string.IsNullOrWhiteSpace(options.BundleId), "Payment:Apple:BundleId 未配置。");
        Assert.False(
            string.IsNullOrWhiteSpace(options.AppStoreServerApi.IssuerId),
            "Payment:Apple:AppStoreServerApi:IssuerId 未配置。");
        Assert.False(
            string.IsNullOrWhiteSpace(options.AppStoreServerApi.KeyId),
            "Payment:Apple:AppStoreServerApi:KeyId 未配置。");
        Assert.False(
            string.IsNullOrWhiteSpace(options.AppStoreServerApi.PrivateKeyPem),
            "Payment:Apple:AppStoreServerApi:PrivateKeyPem 未配置。");

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var client = new AppStoreServerApiClient(
            signingKey: options.AppStoreServerApi.PrivateKeyPem.Trim(),
            keyId: options.AppStoreServerApi.KeyId.Trim(),
            issuerId: options.AppStoreServerApi.IssuerId.Trim(),
            bundleId: options.BundleId.Trim(),
            environment: AppleStoreEnvironmentResolver.Resolve(environmentName: options.Environment),
            httpClient: httpClient);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            client.GetTransactionInfo(transactionId: MissingTransactionId)
                .WaitAsync(TestContext.Current.CancellationToken));

        output.WriteLine(
            $"HttpStatusCode={exception.HttpStatusCode}, "
            + $"ApiErrorCode={exception.ApiErrorCode}, "
            + $"ApiErrorMessage={exception.ApiErrorMessage}");
        Assert.NotEqual(HttpStatusCode.Unauthorized, exception.HttpStatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, exception.HttpStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, exception.HttpStatusCode);
        Assert.Equal(
            "4040010",
            Convert.ToString(exception.ApiErrorCode, CultureInfo.InvariantCulture));
    }

    private static IConfigurationRoot LoadDevelopmentConfiguration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webDirectory = Path.Combine(repositoryRoot, "src", "Bumpic.Web");
        return new ConfigurationBuilder()
            .SetBasePath(webDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bumpic.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("无法定位 Bumpic 仓库根目录。");
    }
}
