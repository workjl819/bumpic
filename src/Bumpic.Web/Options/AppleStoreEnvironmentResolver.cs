using Mimo.AppStoreServerLibrary.Models;

namespace Bumpic.Web.Options;

/// <summary>
/// 将部署配置中的 Apple 环境名称解析为 Mimo SDK 环境对象。
/// </summary>
public static class AppleStoreEnvironmentResolver
{
    private const string SandboxName = "Sandbox";
    private const string ProductionName = "Production";
    private const string LocalTestingName = "LocalTesting";

    /// <summary>
    /// 判断配置中的 Apple 支付环境名称是否受支持；名称忽略大小写和首尾空白。
    /// </summary>
    public static bool IsSupported(string? environmentName)
    {
        return string.IsNullOrWhiteSpace(environmentName)
               || TryNormalize(environmentName: environmentName) is not null;
    }

    /// <summary>
    /// 判断环境是否允许用于真实后端部署；LocalTesting 不执行签名验证，因此禁止部署使用。
    /// </summary>
    public static bool IsDeploymentEnvironment(string? environmentName)
    {
        var normalized = string.IsNullOrWhiteSpace(environmentName)
            ? SandboxName
            : TryNormalize(environmentName: environmentName);
        return normalized is SandboxName or ProductionName;
    }

    /// <summary>
    /// 解析 Apple 支付环境配置；未配置时沿用默认的沙盒环境，未知环境直接失败。
    /// </summary>
    public static AppStoreEnvironment Resolve(string? environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return AppStoreEnvironment.Sandbox;
        }

        return TryNormalize(environmentName: environmentName) switch
        {
            SandboxName => AppStoreEnvironment.Sandbox,
            ProductionName => AppStoreEnvironment.Production,
            LocalTestingName => AppStoreEnvironment.LocalTesting,
            _ => throw new InvalidOperationException(
                $"Apple 支付环境配置 {environmentName} 不受支持，只允许 Sandbox、Production 或 LocalTesting。")
        };
    }

    /// <summary>
    /// 解析 Apple 支付环境的规范名称，用于与商店载荷中的环境字段比较。
    /// </summary>
    public static string ResolveName(string? environmentName)
    {
        return Resolve(environmentName: environmentName).Name;
    }

    private static string? TryNormalize(string? environmentName)
    {
        var value = environmentName?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Equals(SandboxName, StringComparison.OrdinalIgnoreCase))
        {
            return SandboxName;
        }

        if (value.Equals(ProductionName, StringComparison.OrdinalIgnoreCase))
        {
            return ProductionName;
        }

        return value.Equals(LocalTestingName, StringComparison.OrdinalIgnoreCase)
            ? LocalTestingName
            : null;
    }
}
