using System.Text.Json.Serialization;
using Bumpic.Web.Application.Queries.Registration;
using Bumpic.Web.Services.SessionTokens;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 快捷登录接口本次执行的动作。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalIdentityAuthenticationAction
{
    /// <summary>该平台身份此前已绑定，本次为登录。</summary>
    LoggedIn,

    /// <summary>该平台身份此前未绑定，但令牌邮箱已注册，本次绑定到该邮箱账户。</summary>
    Bound,

    /// <summary>该平台身份此前未绑定且令牌邮箱尚未注册，本次先注册账户再完成绑定。</summary>
    Registered
}

/// <summary>
/// 邀请码补交窗口信息；仅当本次动作是 Registered（自动注册了新账户）时返回。
/// </summary>
/// <param name="Token">窗口令牌；在该窗口内可携带邀请码调用提交邀请码接口。</param>
/// <param name="ExpiresInSeconds">窗口剩余有效秒数（默认 10 分钟）。</param>
public record InvitationWindowInfo(Guid Token, int ExpiresInSeconds);

/// <summary>
/// 快捷登录/绑定响应。
/// </summary>
/// <param name="Action">
/// 本次动作：LoggedIn 表示该平台身份已绑定、本次直接登录；Bound 表示该平台身份未绑定、本次绑定到令牌邮箱对应的已有账户；
/// Registered 表示该平台身份未绑定且令牌邮箱尚未注册，本次注册账户并绑定。
/// </param>
/// <param name="Session">会话令牌与账户信息，结构与注册、邮箱登录接口一致。</param>
/// <param name="InvitationWindow">
/// 邀请码补交窗口：自动注册新账户时返回，客户端据此弹窗引导用户填写邀请码并在窗口内提交；
/// 已绑定登录或绑定到既有账户时为 null。
/// </param>
public record ExternalIdentityAuthenticationResponse(
    ExternalIdentityAuthenticationAction Action,
    AuthenticationSessionResponse Session,
    InvitationWindowInfo? InvitationWindow = null)
{
    /// <summary>
    /// 由动作、令牌与账户信息装配响应。
    /// </summary>
    /// <param name="action">本次动作。</param>
    /// <param name="tokens">会话令牌。</param>
    /// <param name="account">账户信息。</param>
    /// <param name="invitationWindow">邀请码补交窗口；仅自动注册时提供。</param>
    /// <returns>快捷登录/绑定响应。</returns>
    public static ExternalIdentityAuthenticationResponse Build(
        ExternalIdentityAuthenticationAction action,
        SessionTokens tokens,
        GetUserAccountByEmailResponse account,
        InvitationWindowInfo? invitationWindow = null)
    {
        return new ExternalIdentityAuthenticationResponse(
            action,
            AuthenticationSessionResponseFactory.Build(tokens, account),
            invitationWindow);
    }
}
