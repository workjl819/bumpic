using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 基于 SMTP 的验证码发送实现；日志不记录完整邮箱与验证码。
/// </summary>
public class SmtpEmailCodeSender(
    IOptions<EmailSenderOptions> senderOptions,
    IOptions<EmailCodeOptions> codeOptions,
    ILogger<SmtpEmailCodeSender> logger) : IEmailCodeSender
{
    /// <inheritdoc />
    public async Task SendAsync(string toAddress, OperationType purpose, string code, CancellationToken cancellationToken)
    {
        var sender = senderOptions.Value;
        var validMinutes = Math.Max(1, codeOptions.Value.CodeTtlSeconds / 60);
        using var message = new MailMessage(
            from: new MailAddress(sender.SenderAddress, sender.SenderName),
            to: new MailAddress(toAddress))
        {
            Subject = string.IsNullOrWhiteSpace(sender.Subject)
                ? $"{sender.SenderName} verification code"
                : sender.Subject,
            Body = EmailCodeTemplates.RenderHtml(code, sender.SenderName, sender.WebsiteUrl, sender.SupportEmail, validMinutes),
            IsBodyHtml = true
        };
        using var client = new SmtpClient(sender.EmailServerHost, sender.EmailServerPort)
        {
            EnableSsl = sender.EnableSsl
        };
        if (!string.IsNullOrWhiteSpace(sender.UserName))
        {
            client.Credentials = new NetworkCredential(sender.UserName, sender.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("验证码邮件已发送：用途 {Purpose}", purpose);
    }
}
