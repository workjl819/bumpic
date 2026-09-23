using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Apple 支付环境解析测试。
/// </summary>
public class AppleStoreEnvironmentResolverTests
{
    /// <summary>
    /// 受支持的环境名称解析为对应的 Mimo SDK 环境。
    /// </summary>
    [Theory]
    [InlineData("Sandbox", "Sandbox")]
    [InlineData("Production", "Production")]
    [InlineData("LocalTesting", "LocalTesting")]
    [InlineData(" production ", "Production")]
    public void ResolveName_Should_Map_Supported_Environment(string configured, string expected)
    {
        Assert.True(AppleStoreEnvironmentResolver.IsSupported(configured));
        Assert.Equal(expected, AppleStoreEnvironmentResolver.ResolveName(configured));
    }

    /// <summary>
    /// 未配置环境时按沙盒环境处理。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveName_Should_Default_To_Sandbox(string? configured)
    {
        Assert.True(AppleStoreEnvironmentResolver.IsSupported(configured));
        Assert.Equal("Sandbox", AppleStoreEnvironmentResolver.ResolveName(configured));
    }

    /// <summary>
    /// 未知环境名称不受支持且解析时抛错。
    /// </summary>
    [Fact]
    public void Resolve_Should_Reject_Unsupported_Environment()
    {
        Assert.False(AppleStoreEnvironmentResolver.IsSupported("Xcode"));
        Assert.Throws<InvalidOperationException>(() =>
            AppleStoreEnvironmentResolver.Resolve("Xcode"));
    }

    /// <summary>
    /// LocalTesting 可供 SDK 测试工具解析，但不能作为后端部署环境。
    /// </summary>
    [Fact]
    public void Deployment_Should_Reject_LocalTesting()
    {
        Assert.True(AppleStoreEnvironmentResolver.IsSupported("LocalTesting"));
        Assert.False(AppleStoreEnvironmentResolver.IsDeploymentEnvironment("LocalTesting"));
    }
}
