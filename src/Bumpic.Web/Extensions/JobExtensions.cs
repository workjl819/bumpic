using System.Linq.Expressions;
using Hangfire;
using Hangfire.Annotations;
using Hangfire.Common;
using Microsoft.Extensions.Options;

namespace Bumpic.Web.Extensions;

/// <summary>
/// job扩展类
/// </summary>
public static class JobExtensions
{
    /// <summary>
    /// 添加job
    /// </summary>
    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        // services.AddTransient<DemoJob>();

        return services;
    }

    /// <summary>
    /// 注册job
    /// </summary>
    public static void RegisterJobs(this IApplicationBuilder app)
    {
        GlobalConfiguration.Configuration.UseActivator(new ContainerJobActivator(app.ApplicationServices));
        var recurringJobManager = app.ApplicationServices.GetRequiredService<IRecurringJobManager>();
        
        /*recurringJobManager.AddOrUpdate<DemoJob>(
            methodCall: job => job.ExecuteAsync(CancellationToken.None),
            cronExpression: "0 * * * *",
            timeZone: TimeZoneInfo.Utc);*/
    }
}

public static class JobExtension
{
    public static void AddOrUpdate<T>(
        this IRecurringJobManager manager,
        Expression<Func<T, Task>> methodCall,
        [NotNull] string cronExpression,
        [CanBeNull] TimeZoneInfo timeZone,
        [NotNull] string queue = "default")
    {
        var job = Job.FromExpression<T>(methodCall);
        var recurringJobId = job.Type.Name + "." + job.Method.Name;
        manager.AddOrUpdate<T>(recurringJobId, methodCall, cronExpression, new RecurringJobOptions()
        {
            TimeZone = timeZone,
        });
    }

    public static void AddOrUpdate<T>(
        this IRecurringJobManager manager,
        Expression<Func<T, Task>> methodCall,
        [NotNull] Func<string> cronExpression,
        [CanBeNull] TimeZoneInfo timeZone,
        [NotNull] string queue = "default")
    {
        var job = Job.FromExpression<T>(methodCall);
        var recurringJobId = job.Type.Name + "." + job.Method.Name;
        manager.AddOrUpdate<T>(recurringJobId, methodCall, cronExpression(), new RecurringJobOptions()
        {
            TimeZone = timeZone,
        });
    }
}

/// <summary>
/// 容器job激活
/// </summary>
public class ContainerJobActivator : JobActivator
{
#pragma warning disable S2933
    private IServiceProvider _container;

    /// <summary>
    /// 容器job激活
    /// </summary>
    public ContainerJobActivator(IServiceProvider container)
    {
        _container = container;
    }

    /// <summary>
    /// 开始范围
    /// </summary>
#pragma warning disable CS0672 // Member overrides obsolete member
    public override JobActivatorScope BeginScope()
#pragma warning restore CS0672 // Member overrides obsolete member
    {
        return new MyJobActivatorScope(_container.CreateScope());
    }

    /// <summary>
    /// 激活job
    /// </summary>
    public override object ActivateJob(Type jobType)
    {
        return _container.GetRequiredService(jobType);
    }
}

/// <summary>
/// 我的job激活范围
/// </summary>
public class MyJobActivatorScope(IServiceScope scope) : JobActivatorScope
{
    /// <summary>
    /// 处理范围
    /// </summary>
    public override void DisposeScope()
    {
        scope.Dispose();
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    public override object Resolve(Type type)
    {
        return scope.ServiceProvider.GetRequiredService(type);
    }
}
