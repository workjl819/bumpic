using __PROJECT_NAME__.Web.Options;
using NetCorePal.Extensions.NewtonsoftJson;
using NetCorePal.Extensions.ServiceDiscovery;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RabbitMQ.Client;
using Refit;

namespace __PROJECT_NAME__.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMQ(this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        RabbitMQOptions options = new();
        configurationSection.Bind(options);
        services.AddSingleton<IConnectionFactory>(new ConnectionFactory()
        {
            HostName = options.HostName,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.Username,
            Password = options.Password,
            ClientProvidedName = "RivTower.RivDap.Web".ToLower(),
            // DispatchConsumersAsync = true
        });

        return services;
    }

    public static IServiceCollection AddClients(this IServiceCollection services)
    {
        var jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        jsonSerializerSettings.Converters.Add(new NewtonsoftEntityIdJsonConverter());

        var ser = new NewtonsoftJsonContentSerializer(jsonSerializerSettings);

        var settings = new RefitSettings(ser);
        return services;
    }


    public static IHttpClientBuilder AddServiceSelector(this IHttpClientBuilder builder, string serviceName)
    {
        return builder.ConfigureHttpClient((serviceProvider, httpClient) =>
        {
            var serviceSelector = serviceProvider.GetRequiredService<IServiceSelector>();
            var service = serviceSelector.Find(serviceName);
            ArgumentNullException.ThrowIfNull(service);
            httpClient.BaseAddress = new Uri(service.Address);
        });
    }

    public static IServiceCollection AddAllContext(this IServiceCollection services)
    {
        services.AddContext().AddEnvContext(envContextKey: "env").AddCapContextProcessor();
        return services;
    }


    /// <summary>
    /// 注册命名空间RivTower.RivDap.Web.Application.Queries下面的所有Query服务
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddAllQueryService(this IServiceCollection services)
    {
        typeof(Program).Assembly.GetTypes()
            .Where(p => p.Namespace == "Mintpad.Web.Application.Queries" && p.IsClass && !p.IsAbstract).ToList()
            .ForEach(p => { services.AddScoped(p); });
        return services;
    }

    
}