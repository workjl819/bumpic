using Microsoft.AspNetCore.DataProtection;
using Bumpic.Web.Services.ExternalIdentities;

namespace Bumpic.Web.Tests.Services.ExternalIdentities;

/// <summary>
/// 撤销令牌加解密单元测试。
/// </summary>
public class RevocationTokenProtectorTests
{
    /// <summary>
    /// 加密后可以解出原文，且密文不等于原文。
    /// </summary>
    [Fact]
    public void Protect_ThenUnprotect_ReturnsOriginalToken()
    {
        var protector = CreateProtector();

        var ciphertext = protector.Protect("apple-refresh-token");

        Assert.NotEqual("apple-refresh-token", ciphertext);
        Assert.Equal("apple-refresh-token", protector.Unprotect(ciphertext));
    }

    /// <summary>
    /// 空密文返回 null。
    /// </summary>
    [Fact]
    public void Unprotect_EmptyCiphertext_ReturnsNull()
    {
        var protector = CreateProtector();

        Assert.Null(protector.Unprotect(null));
        Assert.Null(protector.Unprotect(string.Empty));
    }

    /// <summary>
    /// 损坏或换密钥环后的密文返回 null，由调用方按无法撤销处理。
    /// </summary>
    [Fact]
    public void Unprotect_CorruptedCiphertext_ReturnsNull()
    {
        var protector = CreateProtector();

        Assert.Null(protector.Unprotect("not-a-valid-ciphertext"));
    }

    /// <summary>
    /// 空令牌不允许加密。
    /// </summary>
    [Fact]
    public void Protect_EmptyToken_Throws()
    {
        var protector = CreateProtector();

        Assert.Throws<ArgumentException>(() => protector.Protect(" "));
    }

    private static RevocationTokenProtector CreateProtector()
    {
        return new RevocationTokenProtector(new EphemeralDataProtectionProvider());
    }
}
