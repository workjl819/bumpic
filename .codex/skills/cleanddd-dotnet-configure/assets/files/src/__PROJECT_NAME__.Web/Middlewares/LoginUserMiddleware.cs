using __PROJECT_NAME__.Web.Services;

namespace __PROJECT_NAME__.Web.Middlewares;

/// <summary>
/// 当前登录用户中间件：将解析出的 LoginUser 放入 HttpContext.Items。
/// </summary>
public class LoginUserMiddleware(RequestDelegate next, ILoginUserService service)
{
    /// <summary>
    /// HttpContext.Items 中 LoginUser 的键。
    /// </summary>
    internal const string LoginUserKey = "__loginUser";

    /// <summary>
    /// 解析并注入当前登录用户。
    /// </summary>
    public async Task Invoke(HttpContext context)
    {
        context.Items[LoginUserKey] = await service.GetLoginUserAsync();
        await next(context);
    }
}