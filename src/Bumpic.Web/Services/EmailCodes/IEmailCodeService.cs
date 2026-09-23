using Bumpic.Domain.Enums;

namespace Bumpic.Web.Services.EmailCodes;

/// <summary>
/// 邮箱验证码服务：发送（限流/冷却/生成/投递）与核销；供 Endpoint 直接调用，不经过 Command。
/// </summary>
public interface IEmailCodeService
{
    /// <summary>
    /// 发送验证码。
    /// </summary>
    /// <param name="email">规范化后的邮箱。</param>
    /// <param name="purpose">验证码用途。</param>
    /// <param name="ipAddress">客户端 IP，用于限流。</param>
    /// <param name="deviceId">客户端设备标识，用于限流辅助。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>重发冷却秒数。</returns>
    Task<int> SendCodeAsync(
        string email,
        OperationType purpose,
        string? ipAddress,
        string? deviceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 核销验证码；不匹配或超过尝试上限时抛出稳定业务错误。
    /// </summary>
    /// <param name="email">规范化后的邮箱。</param>
    /// <param name="purpose">验证码用途。</param>
    /// <param name="code">明文验证码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task VerifyAndConsumeAsync(
        string email,
        OperationType purpose,
        string code,
        CancellationToken cancellationToken);
}
