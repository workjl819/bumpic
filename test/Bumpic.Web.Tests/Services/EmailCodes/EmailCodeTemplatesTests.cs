using Bumpic.Web.Services.EmailCodes;

namespace Bumpic.Web.Tests.Services.EmailCodes;

/// <summary>
/// 验证码邮件模板渲染单元测试。
/// </summary>
public class EmailCodeTemplatesTests
{
    /// <summary>
    /// HTML 正文包含验证码、产品名、有效期与官网地址。
    /// </summary>
    [Fact]
    public void RenderHtml_ContainsCodeProductValidityAndWebsite()
    {
        var html = EmailCodeTemplates.RenderHtml("123456", "Photo Rescue", "https://lumavill.com", "support@lumavill.com", 10);

        Assert.Contains("123456", html);
        Assert.Contains("Photo Rescue", html);
        Assert.Contains("valid for 10 minutes", html);
        Assert.Contains("https://lumavill.com", html);
        Assert.Contains("support@lumavill.com", html);
        Assert.DoesNotContain("${", html);
    }

    /// <summary>
    /// 产品名与官网地址做 HTML 转义，避免注入。
    /// </summary>
    [Fact]
    public void RenderHtml_EncodesProductNameAndWebsite()
    {
        var html = EmailCodeTemplates.RenderHtml("123456", "A & B <script>", "https://example.com/?a=1&b=2", "help@example.com", 5);

        Assert.Contains("A &amp; B &lt;script&gt;", html);
        Assert.Contains("https://example.com/?a=1&amp;b=2", html);
        Assert.DoesNotContain("<script>", html);
    }

    /// <summary>
    /// 纯文本正文包含验证码与产品名。
    /// </summary>
    [Fact]
    public void RenderText_ContainsCodeAndProduct()
    {
        var text = EmailCodeTemplates.RenderText("654321", "Photo Rescue", 10);

        Assert.Contains("654321", text);
        Assert.Contains("Photo Rescue", text);
        Assert.Contains("valid for 10 minutes", text);
    }
}