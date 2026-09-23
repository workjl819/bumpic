using System.Net;
using System.Text;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 验证码邮件模板（内嵌，占位符替换）。
/// </summary>
public static class EmailCodeTemplates
{
    /// <summary>
    /// HTML 模板，占位符：产品名、验证码、有效期分钟、官网地址。
    /// </summary>
    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>${product-name} verification code</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#222222;">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f5f5f5;">
            <tr>
              <td align="center" style="padding:24px 12px;">
                <table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:600px;background-color:#ffffff;border:1px solid #e5e7eb;">
                  <tr>
                    <td style="background-color:#2563eb;padding:16px 24px;">
                      <p style="margin:0;font-size:18px;font-weight:bold;color:#ffffff;">${product-name}</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:28px 24px 8px 24px;">
                      <p style="margin:0 0 16px 0;font-size:18px;font-weight:bold;color:#111827;">Verification code</p>
                      <p style="margin:0 0 20px 0;font-size:14px;color:#374151;">Use the following code to complete your verification. The code is valid for ${valid-minutes} minutes.</p>
                      <p style="margin:0 0 20px 0;">
                        <span style="display:inline-block;background-color:#f3f4f6;border:1px solid #d1d5db;padding:14px 20px;font-size:28px;font-weight:bold;letter-spacing:4px;color:#111827;">${verify-code}</span>
                      </p>
                      <p style="margin:0;font-size:13px;color:#6b7280;">If you did not request this code, you can safely ignore this email.</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:20px 24px 24px 24px;">
                      <p style="margin:0;border-top:1px solid #e5e7eb;padding-top:16px;font-size:12px;color:#6b7280;">
                        This is an automated message. Please do not reply directly to this email.<br />
                        Website: <a href="${website-url}" style="color:#2563eb;">${website-url}</a><br />
                        Need help? Contact <a href="mailto:${support-email}" style="color:#2563eb;">${support-email}</a>
                      </p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    /// <summary>
    /// 渲染 HTML 正文。
    /// </summary>
    /// <param name="code">明文验证码。</param>
    /// <param name="productName">产品名（发件人名称）。</param>
    /// <param name="websiteUrl">官网地址。</param>
    /// <param name="supportEmail">联系（投诉）邮箱。</param>
    /// <param name="validMinutes">有效期（分钟）。</param>
    /// <returns>HTML 正文。</returns>
    public static string RenderHtml(string code, string productName, string websiteUrl, string supportEmail, int validMinutes)
    {
        return HtmlTemplate
            .Replace("${product-name}", WebUtility.HtmlEncode(productName), StringComparison.Ordinal)
            .Replace("${verify-code}", WebUtility.HtmlEncode(code), StringComparison.Ordinal)
            .Replace("${valid-minutes}", validMinutes.ToString(), StringComparison.Ordinal)
            .Replace("${website-url}", WebUtility.HtmlEncode(websiteUrl), StringComparison.Ordinal)
            .Replace("${support-email}", WebUtility.HtmlEncode(supportEmail), StringComparison.Ordinal);
    }

    /// <summary>
    /// 渲染纯文本正文（不支持 HTML 的客户端兜底）。
    /// </summary>
    /// <param name="code">明文验证码。</param>
    /// <param name="productName">产品名（发件人名称）。</param>
    /// <param name="validMinutes">有效期（分钟）。</param>
    /// <returns>纯文本正文。</returns>
    public static string RenderText(string code, string productName, int validMinutes)
    {
        var builder = new StringBuilder();
        builder.Append("Your ").Append(productName).Append(" verification code is ").Append(code).Append('.');
        builder.Append(" The code is valid for ").Append(validMinutes).Append(" minutes.");
        return builder.ToString();
    }
}