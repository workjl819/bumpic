using Hangfire.Dashboard;

namespace Bumpic.Web.Utils;

public class HangfireNoAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}