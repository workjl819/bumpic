namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 邮件发送配置（配置节 <c>Email</c>）。
/// </summary>
public class EmailSenderOptions
{
    /// <summary>
    /// 发件人名称，同时用于邮件中的产品名。
    /// </summary>
    public string SenderName { get; set; } = "Bumpic";

    /// <summary>
    /// 发件人地址。
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;

    /// <summary>
    /// 邮件主题；为空时按发件人名称生成默认主题。
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 邮件中展示的官网地址。
    /// </summary>
    public string WebsiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// 邮件中展示的联系（投诉）邮箱。
    /// </summary>
    public string SupportEmail { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 服务器域名。
    /// </summary>
    public string EmailServerHost { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 服务器端口。
    /// </summary>
    public int EmailServerPort { get; set; }

    /// <summary>
    /// SMTP 用户名；为空时跳过认证。
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 密码。
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用 SMTP TLS。
    /// </summary>
    public bool EnableSsl { get; set; } = true;
}
