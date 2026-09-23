namespace Bumpic.Domain;

/// <summary>
/// 邮箱规范化工具：统一注册、登录、验证码等链路的邮箱规范化规则。
/// </summary>
public static class EmailAddressNormalizer
{
    /// <summary>
    /// 规范化邮箱：去除首尾空白并转换为小写（不变区域性）。
    /// </summary>
    /// <param name="emailAddress">原始邮箱；null 或空白统一按业务错误处理，避免冒泡成 500。</param>
    /// <returns>规范化后的邮箱。</returns>
    /// <exception cref="KnownException"><c>EMAIL_REQUIRED</c>：邮箱为空或空白。</exception>
    public static string Normalize(string? emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            throw new KnownException("EMAIL_REQUIRED");
        }

        return emailAddress.Trim().ToLowerInvariant();
    }
}
