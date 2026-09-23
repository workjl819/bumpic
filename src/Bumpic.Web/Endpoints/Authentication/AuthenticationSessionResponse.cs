using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Queries.Registration;
using Bumpic.Web.Services.SessionTokens;

namespace Bumpic.Web.Endpoints.Authentication;

/// <summary>
/// 登录类接口通用会话响应（注册、邮箱登录、快捷登录共用）。
/// </summary>
/// <param name="AccessToken">访问令牌（JWT），请求时放入 Authorization: Bearer。</param>
/// <param name="RefreshToken">刷新令牌，用于刷新与注销；刷新会轮换，旧值失效。</param>
/// <param name="ExpiresInSeconds">访问令牌剩余有效秒数。</param>
/// <param name="UserId">用户账户标识。</param>
/// <param name="EmailAddress">登录邮箱（注册或登录时使用的邮箱）。</param>
/// <param name="InvitationCode">本人邀请码，可直接分享。</param>
public record AuthenticationSessionResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    Guid UserId,
    string EmailAddress,
    string InvitationCode);

/// <summary>
/// 会话响应装配。
/// </summary>
public static class AuthenticationSessionResponseFactory
{
    /// <summary>
    /// 由令牌与账户信息装配响应。
    /// </summary>
    /// <param name="tokens">会话令牌。</param>
    /// <param name="account">账户信息。</param>
    /// <returns>会话响应。</returns>
    public static AuthenticationSessionResponse Build(SessionTokens tokens, GetUserAccountByEmailResponse account)
    {
        if (account.UserId is null)
        {
            throw new KnownException("USER_NOT_FOUND");
        }

        return new AuthenticationSessionResponse(
            AccessToken: tokens.AccessToken,
            RefreshToken: tokens.RefreshToken,
            ExpiresInSeconds: tokens.ExpiresInSeconds,
            UserId: account.UserId.Id,
            EmailAddress: account.EmailAddress,
            InvitationCode: account.InvitationCode);
    }
}
