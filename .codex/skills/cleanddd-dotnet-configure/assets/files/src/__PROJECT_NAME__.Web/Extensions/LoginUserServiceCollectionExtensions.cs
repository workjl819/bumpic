using __PROJECT_NAME__.Web.Middlewares;
using __PROJECT_NAME__.Web.Services;
using __PROJECT_NAME__.Web.Shared;

namespace __PROJECT_NAME__.Web.Extensions;

/// <summary>
/// 当前登录用户服务注册扩展。
/// </summary>
public static class LoginUserServiceCollectionExtensions
{
    /// <summary>
    /// 注册 LoginUser：单例解析服务 + Scoped 注入当前请求的登录用户。
    /// </summary>
    public static IServiceCollection AddLoginUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ILoginUserService, LoginUserService>();
        services.AddScoped<ILoginUser>(provider =>
        {
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            var user = httpContextAccessor.HttpContext?.Items[LoginUserMiddleware.LoginUserKey];
            if (user is not ILoginUser loginUser)
            {
                return new LoginUser(Guid.Empty, string.Empty, string.Empty);
            }

            return loginUser;
        });
        return services;
    }
}
