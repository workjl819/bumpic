namespace Bumpic.Domain.Tests;

/// <summary>
/// 邀请码生成器单元测试。
/// </summary>
public class InvitationCodeGeneratorTests
{
    /// <summary>
    /// 默认生成 12 位且仅包含无歧义字符集。
    /// </summary>
    [Fact]
    public void Generate_WithDefaultLength_Returns12UnambiguousChars()
    {
        var code = InvitationCodeGenerator.Generate();

        Assert.Equal(InvitationCodeGenerator.DefaultLength, code.Length);
        Assert.All(code, c => Assert.Contains(c, InvitationCodeGenerator.Alphabet));
    }

    /// <summary>
    /// 两次生成结果互不相同。
    /// </summary>
    [Fact]
    public void Generate_Twice_ReturnsDifferentCodes()
    {
        Assert.NotEqual(InvitationCodeGenerator.Generate(), InvitationCodeGenerator.Generate());
    }

    /// <summary>
    /// 字符集不包含易混淆字符。
    /// </summary>
    [Fact]
    public void Alphabet_ExcludesAmbiguousCharacters()
    {
        Assert.DoesNotContain('0', InvitationCodeGenerator.Alphabet);
        Assert.DoesNotContain('O', InvitationCodeGenerator.Alphabet);
        Assert.DoesNotContain('1', InvitationCodeGenerator.Alphabet);
        Assert.DoesNotContain('l', InvitationCodeGenerator.Alphabet);
        Assert.DoesNotContain('I', InvitationCodeGenerator.Alphabet);
    }

    /// <summary>
    /// 非正长度被拒绝。
    /// </summary>
    [Fact]
    public void Generate_WithNonPositiveLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InvitationCodeGenerator.Generate(0));
    }
}
