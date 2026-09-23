using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Bumpic.Web.Services;

namespace Bumpic.Web.Tests.Services;

/// <summary>
/// 当前登录用户解析服务测试。
/// </summary>
public class LoginUserServiceTests
{
    /// <summary>
    /// 验证不包含业务用户标识的 Google 服务账号身份不会被当作登录用户解析。
    /// </summary>
    [Fact]
    public async Task GetLoginUserAsync_Should_Return_Guest_When_Authenticated_Principal_Has_No_Uid()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("email", "pubsub-push@example.iam.gserviceaccount.com"),
                    new Claim("email_verified", "true")
                ],
                authenticationType: "GooglePubSub"))
        };
        var service = CreateService(httpContext: httpContext);

        var result = await service.GetLoginUserAsync();

        Assert.Equal(Guid.Empty, result.Id);
        Assert.Empty(result.Token);
        Assert.Empty(result.Email);
    }

    /// <summary>
    /// 验证包含业务用户标识的身份仍能正常解析。
    /// </summary>
    [Fact]
    public async Task GetLoginUserAsync_Should_Return_Login_User_When_Uid_Is_Valid()
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("uid", userId.ToString()),
                    new Claim("email", "user@example.com")
                ],
                authenticationType: "Bearer"))
        };
        httpContext.Request.Headers.Authorization = "Bearer access-token";
        var service = CreateService(httpContext: httpContext);

        var result = await service.GetLoginUserAsync();

        Assert.Equal(userId, result.Id);
        Assert.Equal("access-token", result.Token);
        Assert.Equal("user@example.com", result.Email);
    }

    private static LoginUserService CreateService(HttpContext httpContext)
    {
        return new LoginUserService(
            httpContextAccessor: new HttpContextAccessor { HttpContext = httpContext },
            logger: NullLogger<LoginUserService>.Instance);
    }
}
