namespace Bumpic.Web.Shared;

/// <summary>
/// 当前登录用户；未认证时为游客（Id 为 Guid.Empty）。
/// </summary>
public interface ILoginUser
{
    /// <summary>
    /// 登录用户标识；游客为 Guid.Empty。
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 访问令牌。
    /// </summary>
    string Token { get; }

    /// <summary>
    /// 用户邮箱（声明中不存在时为空）。
    /// </summary>
    string Email { get; }
}

/// <summary>
/// 当前登录用户实现。
/// </summary>
public record LoginUser(Guid Id, string Token, string Email) : ILoginUser;
