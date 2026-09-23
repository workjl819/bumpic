namespace Bumpic.Domain.Tests;

/// <summary>
/// 邮箱规范化工具单元测试。
/// </summary>
public class EmailAddressNormalizerTests
{
    /// <summary>
    /// 去除首尾空白并统一小写。
    /// </summary>
    [Fact]
    public void Normalize_WithWhitespaceAndMixedCase_TrimsAndLowercases()
    {
        Assert.Equal("user@example.com", EmailAddressNormalizer.Normalize("  User@Example.COM  "));
    }

    /// <summary>
    /// 已规范化邮箱保持不变。
    /// </summary>
    [Fact]
    public void Normalize_WithNormalizedEmail_ReturnsUnchanged()
    {
        Assert.Equal("a@b.com", EmailAddressNormalizer.Normalize("a@b.com"));
    }

    /// <summary>
    /// 传入 null 或空白时抛出业务错误 EMAIL_REQUIRED（避免冒泡成 500）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WithNullOrBlank_ThrowsKnownException(string? emailAddress)
    {
        var exception = Assert.Throws<KnownException>(() => EmailAddressNormalizer.Normalize(emailAddress));

        Assert.Equal("EMAIL_REQUIRED", exception.Message);
    }
}
