using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Exceptions;
using Mimo.AppStoreServerLibrary.Models;
using Bumpic.Web.Options;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 基于 Mimo.AppStoreServerLibrary 的 Apple JWS 验签适配器。
/// </summary>
public sealed class AppleSignedDataPayloadVerifier(IOptions<AppleStoreOptions> options)
{
    private const string SandboxEnvironmentName = "Sandbox";
    private const string ProductionEnvironmentName = "Production";
    private const string LocalTestingEnvironmentName = "LocalTesting";

    /// <summary>
    /// 校验 Apple 服务端通知外层 JWS 并返回已验证载荷。
    /// </summary>
    public async Task<AppleSignedPayload> VerifyNotificationPayloadAsync(
        string signedPayload,
        CancellationToken cancellationToken)
    {
        var rawPayload = ReadUnverifiedPayloadForVerification(jws: signedPayload);
        var data = ReadRequiredObject(element: rawPayload, propertyName: "data");
        var bundleId = RequireValue(
            value: ReadOptionalString(element: data, propertyName: "bundleId"),
            fieldName: "data.bundleId");
        var environmentName = RequireValue(
            value: ReadOptionalString(element: data, propertyName: "environment"),
            fieldName: "data.environment");
        var environment = ResolvePayloadEnvironment(environmentName: environmentName);
        await ExecuteAsync(
            action: verifier => verifier.VerifyAndDecodeNotification(signedPayload),
            configuration: options.Value,
            bundleId: bundleId,
            environment: environment,
            cancellationToken: cancellationToken);
        return CreateSignedPayload(signedPayload: signedPayload);
    }

    /// <summary>
    /// 校验 Apple 签名交易 JWS 并返回已验证载荷。
    /// </summary>
    public async Task<AppleSignedPayload> VerifyTransactionPayloadAsync(
        string signedPayload,
        CancellationToken cancellationToken)
    {
        var decoded = await ExecuteAsync(
            action: verifier => verifier.VerifyAndDecodeTransaction(signedPayload),
            configuration: options.Value,
            cancellationToken: cancellationToken);
        ValidateDeployment(
            bundleId: decoded.BundleId,
            environment: decoded.Environment,
            configuration: options.Value);
        return CreateSignedPayload(signedPayload: signedPayload);
    }

    private static async Task<TDecoded> ExecuteAsync<TDecoded>(
        Func<SignedDataVerifier, Task<TDecoded>> action,
        AppleStoreOptions configuration,
        CancellationToken cancellationToken,
        string? bundleId = null,
        AppStoreEnvironment? environment = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var verifier = CreateVerifier(
                configuration: configuration,
                bundleId: bundleId,
                environment: environment);
            return await action(verifier).WaitAsync(cancellationToken);
        }
        catch (VerificationException exception)
        {
            throw Invalid(message: "Apple 签名验证失败。", innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Apple 服务暂时不可用。",
                innerException: exception);
        }
        catch (JsonException exception)
        {
            throw Invalid(message: "Apple 签名载荷不是合法 JSON。", innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Apple 支付环境配置不合法。",
                innerException: exception);
        }
    }

    private static SignedDataVerifier CreateVerifier(
        AppleStoreOptions configuration,
        string? bundleId = null,
        AppStoreEnvironment? environment = null)
    {
        if (string.IsNullOrWhiteSpace(configuration.BundleId))
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Apple Bundle ID 未配置。");
        }

        var resolvedEnvironment = environment ?? AppleStoreEnvironmentResolver.Resolve(
            environmentName: configuration.Environment);
        if (string.Equals(
                resolvedEnvironment.Name,
                LocalTestingEnvironmentName,
                StringComparison.Ordinal))
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Apple 本地测试环境不校验签名，禁止用于验单。");
        }

        var rootCertificates = configuration.RootCertificatesPem
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x =>
            {
                try
                {
                    return X509Certificate2
                        .CreateFromPem(x)
                        .Export(X509ContentType.Cert);
                }
                catch (CryptographicException exception)
                {
                    throw new StoreClientException(
                        code: "STORE_SERVICE_UNAVAILABLE",
                        isRetryable: true,
                        message: "Apple 根证书配置不合法。",
                        innerException: exception);
                }
            })
            .ToArray();
        if (rootCertificates.Length == 0)
        {
            throw new StoreClientException(
                code: "STORE_SERVICE_UNAVAILABLE",
                isRetryable: true,
                message: "Apple 根证书未配置，无法完成验签。");
        }

        return new SignedDataVerifier(
            appleRootCertificates: rootCertificates,
            enableOnlineChecks: configuration.EnableOnlineChecks,
            environment: resolvedEnvironment,
            bundleId: bundleId?.Trim() ?? configuration.BundleId.Trim());
    }

    private static void ValidateDeployment(
        string? bundleId,
        string? environment,
        AppleStoreOptions configuration)
    {
        if (!string.Equals(bundleId, configuration.BundleId.Trim(), StringComparison.Ordinal))
        {
            throw Invalid(message: "Apple 签名载荷不属于当前部署应用。");
        }

        if (!string.Equals(
                environment,
                AppleStoreEnvironmentResolver.ResolveName(
                    environmentName: configuration.Environment),
                StringComparison.Ordinal))
        {
            throw Invalid(message: "Apple 签名载荷环境与当前部署不一致。");
        }

        if (string.Equals(environment, SandboxEnvironmentName, StringComparison.Ordinal)
            || string.Equals(environment, ProductionEnvironmentName, StringComparison.Ordinal))
        {
            return;
        }

        throw Invalid(message: "Apple 签名载荷环境不受支持。");
    }

    /// <summary>
    /// 返回已完成签名校验的 Apple 原始载荷，保留 SDK 模型尚未建模的审计字段。
    /// </summary>
    private static AppleSignedPayload CreateSignedPayload(string signedPayload)
    {
        return new AppleSignedPayload(
            Payload: ReadRawPayload(jws: signedPayload),
            PayloadHash: ComputePayloadHash(jws: signedPayload),
            SourcePrincipal: ComputeSourcePrincipal(jws: signedPayload));
    }

    private static JsonElement ReadRawPayload(string jws)
    {
        using var document = JsonDocument.Parse(ReadPayloadSegment(jws: jws));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 只提取创建 SDK 验证器所需的路由字段；解析结果必须随后通过签名验证。
    /// </summary>
    private static JsonElement ReadUnverifiedPayloadForVerification(string jws)
    {
        try
        {
            return ReadRawPayload(jws: jws);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw Invalid(message: "Apple 签名载荷不是合法 JWS JSON。", innerException: exception);
        }
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static JsonElement ReadRequiredObject(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        throw Invalid(message: $"Apple 签名载荷缺少 {propertyName}。");
    }

    private static AppStoreEnvironment ResolvePayloadEnvironment(string environmentName)
    {
        return environmentName switch
        {
            SandboxEnvironmentName => AppStoreEnvironment.Sandbox,
            ProductionEnvironmentName => AppStoreEnvironment.Production,
            _ => throw Invalid(message: "Apple 签名载荷环境不受支持。")
        };
    }

    private static string RequireValue(string? value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw Invalid(message: $"Apple 签名载荷缺少 {fieldName}。")
            : value;
    }

    private static StoreClientException Invalid(
        string message,
        Exception? innerException = null)
    {
        return innerException is null
            ? new StoreClientException(code: "PURCHASE_INVALID", isRetryable: false, message: message)
            : new StoreClientException(
                code: "PURCHASE_INVALID",
                isRetryable: false,
                message: message,
                innerException: innerException);
    }

    /// <summary>
    /// 计算签名载荷摘要，用于幂等、审计和重放比对。
    /// </summary>
    private static string ComputePayloadHash(string jws)
    {
        return CanonicalEncoding.EncodeBase64Url(
            value: SHA256.HashData(ReadPayloadSegment(jws: jws)));
    }

    /// <summary>
    /// 计算已完成验签的叶子证书主体摘要，不保存完整凭据。
    /// </summary>
    private static string ComputeSourcePrincipal(string jws)
    {
        var header = JsonDocument.Parse(ReadHeaderSegment(jws: jws));
        if (!header.RootElement.TryGetProperty("x5c", out var certificateChain)
            || certificateChain.ValueKind != JsonValueKind.Array
            || certificateChain.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var leafCertificate = Convert.FromBase64String(
            certificateChain[0].GetString() ?? string.Empty);
        return CanonicalEncoding.EncodeBase64Url(
            value: SHA256.HashData(leafCertificate));
    }

    private static byte[] ReadPayloadSegment(string jws)
    {
        var segments = SplitJws(jws: jws);
        return Base64UrlEncoder.DecodeBytes(segments[1]);
    }

    private static byte[] ReadHeaderSegment(string jws)
    {
        var segments = SplitJws(jws: jws);
        return Base64UrlEncoder.DecodeBytes(segments[0]);
    }

    private static string[] SplitJws(string jws)
    {
        var segments = jws.Split('.');
        return segments.Length == 3
            ? segments
            : throw Invalid(message: "Apple JWS 格式不合法。");
    }
}
