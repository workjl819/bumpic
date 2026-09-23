using __PROJECT_NAME__.Web.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Moq;

namespace __PROJECT_NAME__.Web.Tests.Extensions;

public class MyWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly TestContainerFixture Containers = new();
    
    private static int index = 0;
    
    private readonly int i;

    static MyWebApplicationFactory()
    {
        NewtonsoftJsonDefaults.DefaultOptions.Converters.Add(new NewtonsoftEntityIdJsonConverter());
    }

    public async ValueTask InitializeAsync()
    {
        await Containers.CreateVisualHostAsync(TestVirtualHost);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    public MyWebApplicationFactory()
    {
        lock (Containers)
        {
            i = index++;
        }
    }
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:Port", Containers.RedisContainer.GetMappedPublicPort(6379).ToString());
        builder.UseSetting("Redis:Host", Containers.RedisContainer.Hostname);
        builder.UseSetting("Redis:Database", "0");
        builder.UseSetting("ConnectionStrings:MySql",
            Containers.MySqlContainer.GetConnectionString()
                .Replace("demo1", $"demo{i}") .Replace("mysql", "mysql") + ";Max Pool Size=5");
        builder.UseSetting("RabbitMQ:Port", Containers.RabbitMqContainer.GetMappedPublicPort(5672).ToString());
        builder.UseSetting("RabbitMQ:UserName", "guest");
        builder.UseSetting("RabbitMQ:Password", "guest");
        builder.UseSetting("RabbitMQ:VirtualHost", TestVirtualHost);
        builder.UseSetting("RabbitMQ:HostName", Containers.RabbitMqContainer.Hostname);
        builder.UseEnvironment("Development");

        AddJwtConfig(builder);
        SetupMockService(builder);
        base.ConfigureWebHost(builder);
    }

    /// <summary>
    /// 专属工厂可隔离消息虚拟主机。
    /// </summary>
    protected virtual string TestVirtualHost { get; } = "/";

    /// <summary>
    /// 测试实例编号，用于隔离缓存数据库。
    /// </summary>
    protected int TestInstanceIndex { get { return i; } }

    protected virtual void SetupMockService(IWebHostBuilder builder)
    {
    }

    protected virtual Task InitializeServiceAsync()
    {
        return Task.CompletedTask;
    }
    
    private void  AddJwtConfig(IWebHostBuilder builder)
    {
        
        var mockOption = new Mock<IOptions<JwtConfig>>();
        mockOption.Setup(x => x.Value).Returns(new JwtConfig()
        {
            Issuer = "Watt",
            Audience = "WattClient",
            ExpirationInMinutes = 60 * 24 * 7, // 7天
            RefreshTokenExpirationInMinutes = 60 * 24 * 30, // 30天
            SecretKey = "Watt@2024#SecretKey!ForJwtToken"
        });
        
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IOptions<JwtConfig>>(p => mockOption.Object));
        });
    }
}
