using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 邮箱验证码发送端口。
/// </summary>
public interface IEmailCodeSender
{
    /// <summary>
    /// 发送验证码邮件。
    /// </summary>
    /// <param name="toAddress">收件邮箱。</param>
    /// <param name="purpose">验证码用途。</param>
    /// <param name="code">明文验证码，仅用于邮件内容，不持久化。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发送完成的任务。</returns>
    Task SendAsync(string toAddress, OperationType purpose, string code, CancellationToken cancellationToken);
}
