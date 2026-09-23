namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 发布审核邮箱的固定验证码配置。
/// </summary>
public class ForPublishEmailOption
{
    /// <summary>
    /// 是否启用发布审核邮箱固定验证码。
    /// </summary>
    public bool IsOpen { get; set; } = false;

    /// <summary>
    /// 使用固定验证码的发布审核邮箱。
    /// </summary>
    public string PublishEmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// 发布审核邮箱使用的固定验证码。
    /// </summary>
    public string PublishEmailCode { get; set; } = string.Empty;
}
