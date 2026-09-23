using Bumpic.Web.Shared;

namespace Bumpic.Web.Services;

/// <summary>
/// 当前登录用户服务端口。
/// </summary>
public interface ILoginUserService
{
    /// <summary>
    /// 从当前请求解析登录用户。
    /// </summary>
    Task<LoginUser> GetLoginUserAsync();
}

/// <summary>
/// 当前登录用户服务：从认证声明与请求头解析 LoginUser。
/// </summary>
public class LoginUserService(
    IHttpContextAccessor httpContextAccessor,
    ILogger<LoginUserService> logger) : ILoginUserService
{
    /// <inheritdoc />
    public Task<LoginUser> GetLoginUserAsync()
    {
        try
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                logger.LogInformation("HttpContext 为空，返回游客 LoginUser");
                return Task.FromResult(new LoginUser(Guid.Empty, string.Empty, string.Empty));
            }

            var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(' ')[^1];
            if (string.IsNullOrEmpty(token))
            {
                token = context.Request.Query["access_token"];
            }

            var identity = context.User.Identity;
            if (identity is not { IsAuthenticated: true })
            {
                return Task.FromResult(new LoginUser(Guid.Empty, string.Empty, string.Empty));
            }

            var uid = context.User.Claims.FirstOrDefault(claim => claim.Type == "uid")?.Value;
            var email = context.User.Claims.FirstOrDefault(claim => claim.Type == "email")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(uid) || !Guid.TryParse(uid, out var userId))
            {
                return Task.FromResult(new LoginUser(Guid.Empty, string.Empty, string.Empty));
            }

            return Task.FromResult(new LoginUser(userId, token ?? string.Empty, email));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "解析当前登录用户失败");
        }

        return Task.FromResult(new LoginUser(Guid.Empty, string.Empty, string.Empty));
    }
}
