using System.Text.Json;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 已完成 Apple 证书链和签名校验的 JWS 载荷。
/// </summary>
public record AppleSignedPayload(
    JsonElement Payload,
    string PayloadHash,
    string SourcePrincipal);
