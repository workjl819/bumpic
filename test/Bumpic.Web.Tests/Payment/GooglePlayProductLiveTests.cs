using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google Play Console 商品真实连通性测试。
/// </summary>
[Trait("Category", "PaymentLive")]
public class GooglePlayProductLiveTests(ITestOutputHelper output)
{
    private const string LiveTestEnvironmentVariable = "PAYMENT_LIVE_TESTS";

    /// <summary>
    /// 使用开发配置连接 Google Play Developer API 并列出当前应用的内购商品。
    /// </summary>
    [Fact]
    public async Task ListProductsAsync_WithDevelopmentConfiguration_ReturnsGoogleProducts()
    {
        Assert.SkipUnless(
            string.Equals(
                Environment.GetEnvironmentVariable(LiveTestEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase),
            $"默认不访问真实 Google 平台；请设置 {LiveTestEnvironmentVariable}=true 后单独运行。");

        var options = LoadDevelopmentConfiguration()
            .GetSection("Payment:Google")
            .Get<GooglePlayOptions>();
        Assert.NotNull(options);
        Assert.False(string.IsNullOrWhiteSpace(options.PackageName), "Payment:Google:PackageName 未配置。");
        Assert.False(
            string.IsNullOrWhiteSpace(options.ServiceAccountJson),
            "Payment:Google:ServiceAccountJson 未配置。");

        var serviceFactory = new GooglePlayServiceFactory(
            options: Microsoft.Extensions.Options.Options.Create(options));
        using var service = serviceFactory.Create();
        var response = await service.Monetization.Onetimeproducts
            .List(packageName: options.PackageName)
            .ExecuteAsync(TestContext.Current.CancellationToken);
        var products = response.OneTimeProducts ?? [];

        foreach (var product in products)
        {
            output.WriteLine(
                $"ProductId={product.ProductId}, "
                + $"PurchaseOptions={product.PurchaseOptions?.Count ?? 0}");
        }

        Assert.NotEmpty(products);
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
