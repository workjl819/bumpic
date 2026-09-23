using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple 验单配置绑定测试。
/// </summary>
public class AppleStoreOptionsBindingTests
{
    /// <summary>
    /// 配置中的环境名称按原始字符串绑定，不依赖第三方 SDK 类型转换。
    /// </summary>
    [Theory]
    [InlineData("Sandbox")]
    [InlineData("Production")]
    public void Bind_Should_Keep_Environment_As_Configured(string environment)
    {
        var provider = CreateProvider(environment);

        var bound = provider.GetRequiredService<IOptions<AppleStoreOptions>>().Value;

        Assert.Equal(environment, bound.Environment);
        Assert.Equal("com.lumavill.photorescue.dev", bound.BundleId);
        Assert.Equal(123456, bound.AppAppleId);
    }

    /// <summary>
    /// 不受支持的环境名称在读取配置时直接失败，不会静默回落到默认值。
    /// </summary>
    [Theory]
    [InlineData("Xcode")]
    [InlineData("LocalTesting")]
    public void Bind_Should_Fail_For_Unsupported_Environment(string environment)
    {
        var provider = CreateProvider(environment: environment);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<AppleStoreOptions>>().Value);

        Assert.Contains("Apple 支付环境配置不受支持", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider CreateProvider(string environment)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:Apple:BundleId"] = "com.lumavill.photorescue.dev",
                ["Payment:Apple:Environment"] = environment,
                ["Payment:Apple:AppAppleId"] = "123456"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<AppleStoreOptions>()
            .Bind(configuration.GetSection("Payment:Apple"))
            .Validate(
                options => AppleStoreEnvironmentResolver.IsDeploymentEnvironment(options.Environment),
                "Apple 支付环境配置不受支持，后端部署只允许 Sandbox 或 Production。");
        return services.BuildServiceProvider();
    }
}
