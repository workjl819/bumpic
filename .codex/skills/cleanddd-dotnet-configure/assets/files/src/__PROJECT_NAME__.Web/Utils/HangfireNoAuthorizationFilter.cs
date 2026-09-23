using Hangfire.Dashboard;

namespace __PROJECT_NAME__.Web.Utils;

public class HangfireNoAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}