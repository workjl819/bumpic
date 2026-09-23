using Microsoft.AspNetCore.DataProtection;

namespace Bumpic.Web.Services.ExternalIdentities;

/// <summary>
/// 平台撤销令牌的加解密端口。
/// </summary>
public interface IRevocationTokenProtector
{
    /// <summary>
    /// 加密平台撤销令牌。
    /// </summary>
    /// <param name="token">平台返回的 refresh token 等凭据。</param>
    /// <returns>可持久化的密文。</returns>
    string Protect(string token);

    /// <summary>
    /// 解密平台撤销令牌；密钥环失效或密文损坏时返回 null。
    /// </summary>
    /// <param name="ciphertext">持久化的密文。</param>
    /// <returns>明文令牌；无法解密时为 null。</returns>
    string? Unprotect(string? ciphertext);
}

/// <summary>
/// 基于 ASP.NET Data Protection 的撤销令牌加解密实现；密钥环持久化在数据库。
/// </summary>
public class RevocationTokenProtector(IDataProtectionProvider provider) : IRevocationTokenProtector
{
    /// <summary>
    /// Data Protection 用途字符串；更换用途会使既有密文不可解。
    /// </summary>
    private const string Purpose = "Bumpic.ExternalIdentity.RevocationToken";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    /// <inheritdoc />
    public string Protect(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return _protector.Protect(token);
    }

    /// <inheritdoc />
    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (Exception)
        {
            // 密钥环丢失（换库/重置）或密文损坏：按「无法撤销」处理，由调用方记录告警。
            return null;
        }
    }
}
