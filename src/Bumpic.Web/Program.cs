using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using FluentValidation.AspNetCore;
using Bumpic.Web.Clients;
using Bumpic.Web.Extensions;
using Bumpic.Web.Options;
using Bumpic.Web.Services.EmailCodes;
using Bumpic.Web.Services.ExternalIdentities;
using Bumpic.Web.Services.SessionTokens;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Services.Store;
using Bumpic.Web.Services.Invitations;
using Bumpic.Web.Endpoints.StoreNotification;
using Bumpic.Web.Utils;
using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;
using Serilog.Formatting.Json;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;
using NetCorePal.Context;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.MultiEnv;
using Serilog.Events;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .Enrich.WithClientIp()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.AddJsonFile("/app/appconfig.json", optional: true);
    builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
    builder.Services.AddOptions<AppleStoreOptions>()
        .Bind(builder.Configuration.GetSection("Payment:Apple"))
        .Validate(options => AppleStoreEnvironmentResolver.IsDeploymentEnvironment(options.Environment),
            "Apple 支付环境配置不受支持，后端部署只允许 Sandbox 或 Production。")
        .ValidateOnStart();
    builder.Services.Configure<GooglePlayOptions>(builder.Configuration.GetSection("Payment:Google"));
    builder.Services.AddSingleton<IValidateOptions<RewardPointsOptions>, RewardPointsOptionsValidator>();
    builder.Services.AddOptions<RewardPointsOptions>()
        .Bind(builder.Configuration.GetSection("RewardPoints"))
        .ValidateOnStart();
    var appOptions = new AppOptions();
    builder.Configuration.GetSection("App").Bind(appOptions);
    
    
    #region Serilog

    
    if (!builder.Environment.IsDevelopment())
    {
        builder.Logging.ClearProviders();
        builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithClientIp()
                    .MinimumLevel.Override(
                        source: "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware",
                        LogEventLevel.Fatal
                    )
                    .WriteTo.Console(new CompactJsonFormatter());
                configuration
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Error);
            },
            writeToProviders: true);
    }

    #endregion
    
    #region Config
    
    var jwtConfig = new JwtConfig();
    builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection("Jwt"));
    builder.Configuration.GetSection("Jwt").Bind(jwtConfig);
    builder.Services.AddSingleton<JwtGenerator>();
    
    RedisOptions redisOptions = new();

    builder.Configuration.GetSection("Redis").Bind(redisOptions);
    var co = new ConfigurationOptions();
    co.EndPoints.Add(redisOptions.Host, redisOptions.Port);
    co.DefaultDatabase = redisOptions.Database;
    co.Password = redisOptions.Password;
    var redis = await ConnectionMultiplexer.ConnectAsync(co);
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => redis);
    
    var envOption = new EnvOptions();
    builder.Configuration.GetSection("Env").Bind(envOption);
    var displayEnv = string.IsNullOrEmpty(envOption.ServiceEnv) ? "main" : envOption.ServiceEnv;

    var hangfireJobConfiguration = builder.Configuration.GetSection("HangfireJob");
    builder.Services.AddOptions<HangfireJobOptions>().Bind(hangfireJobConfiguration).ValidateOnStart();
    
    #endregion

    #region SignalR

    builder.Services.AddHealthChecks();
    builder.Services.AddMvc()
        .AddNewtonsoftJson(options => { options.SerializerSettings.AddNetCorePalJsonConverters(); });
    builder.Services.AddSignalR();
    

    #endregion

    #region Prometheus监控

    builder.Services.AddHealthChecks().ForwardToPrometheus();
    builder.Services.AddHttpClient(Options.DefaultName)
        .UseHttpClientMetrics();

    #endregion

    // Add services to the container.

    #region 身份认证
    
    // DataProtection - use custom extension that resolves IConnectionMultiplexer from DI
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<ApplicationDbContext>();
    JsonWebKeysOptions certsOptions = new();
    builder.Configuration.Bind(certsOptions);
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddCookie()
        .AddJwtBearer(jwtBearerOptions =>
        {
            jwtBearerOptions.MapInboundClaims = false;
            jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                TryAllIssuerSigningKeys = true,
                ValidAudience = jwtConfig.Audience,
                ValidIssuer = jwtConfig.Issuer,
            };
            jwtBearerOptions.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    // If the request is for our hub...
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/hubs/notify")
                         ||
                         path.StartsWithSegments("/mapteam")))
                    {
                        // Read the token out of the query string
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        })
        .AddJwtBearer(HandleGooglePlayNotificationEndpoint.AuthenticationScheme, jwtBearerOptions =>
        {
            var googlePushOptions = builder.Configuration.GetSection("Payment:Google")
                .Get<GooglePlayOptions>() ?? new GooglePlayOptions();
            jwtBearerOptions.MapInboundClaims = false;
            jwtBearerOptions.Authority = googlePushOptions.PushOidcAuthority;
            jwtBearerOptions.Audience = googlePushOptions.PushAudience;
            jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAudience = googlePushOptions.PushAudience,
                ValidIssuers = ["https://accounts.google.com", "accounts.google.com"]
            };
        });

    // 添加Redis连接
    builder.Services.AddNetCorePalJwt().AddRedisStore(); // 使用Redis存储密钥

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(PolicyNames.Client, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("type", TokenType.Client);
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        })
        .AddPolicy(PolicyNames.Admin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("type", TokenType.Admin);
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        }).AddPolicy(PolicyNames.AdminOnly, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("type", TokenType.Admin);
            policy.RequireRole("admin");
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        }).AddPolicy(PolicyNames.RefreshToken, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("refresh-token", "true");
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        });

    builder.Services.AddLoginUser();
    builder.Services.AddSingleton<SessionTokenIssuer>();
    builder.Services.Configure<EmailCodeOptions>(builder.Configuration.GetSection("EmailCode"));
    builder.Services.Configure<ForPublishEmailOption>(builder.Configuration.GetSection("ForPublishEmail"));
    builder.Services.Configure<EmailSenderOptions>(builder.Configuration.GetSection("Email"));
    builder.Services.AddSingleton<IEmailCodeSender, SmtpEmailCodeSender>();
    builder.Services.AddSingleton<IEmailCodeStore, RedisEmailCodeStore>();
    builder.Services.AddSingleton<IInvitationWindowStore, InvitationWindowStore>();
    builder.Services.AddSingleton<IEmailCodeService, EmailCodeService>();
    builder.Services.AddSingleton<IValidateOptions<AccountDeletionOptions>, AccountDeletionOptionsValidator>();
    builder.Services.AddOptions<AccountDeletionOptions>().Bind(builder.Configuration.GetSection("AccountDeletion")).ValidateOnStart();
    builder.Services.AddMemoryCache();
    builder.Services.Configure<AppleExternalIdentityOptions>(builder.Configuration.GetSection("ExternalIdentity:Apple"));
    builder.Services.Configure<GoogleExternalIdentityOptions>(builder.Configuration.GetSection("ExternalIdentity:Google"));
    var appleAuthBaseUrl = builder.Configuration.GetValue<string>("ExternalIdentity:Apple:BaseUrl") ?? "https://appleid.apple.com";
    var googleAuthBaseUrl = builder.Configuration.GetValue<string>("ExternalIdentity:Google:BaseUrl") ?? "https://www.googleapis.com";
    builder.Services.AddRefitClient<IAppleAuthClient>()
        .ConfigureHttpClient(client => client.BaseAddress = new Uri(appleAuthBaseUrl));
    builder.Services.AddRefitClient<IGoogleAuthClient>()
        .ConfigureHttpClient(client => client.BaseAddress = new Uri(googleAuthBaseUrl));
    builder.Services.AddTransient<PlatformJwksProvider>();
    builder.Services.AddTransient<AppleExternalIdentityVerifier>();
    builder.Services.AddTransient<GoogleExternalIdentityVerifier>();
    builder.Services.AddHttpClient(ExternalIdentityRevocationService.AppleHttpClientName,
        client => client.BaseAddress = new Uri(appleAuthBaseUrl));
    builder.Services.AddHttpClient(ExternalIdentityRevocationService.GoogleOAuthHttpClientName,
        client => client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ExternalIdentity:Google:OAuthBaseUrl") ?? "https://oauth2.googleapis.com"));
    builder.Services.AddSingleton<IRevocationTokenProtector, RevocationTokenProtector>();
    builder.Services.AddScoped<IExternalIdentityRevocationService, ExternalIdentityRevocationService>();
    #endregion


    #region Controller

    builder.Services.AddControllers().AddNetCorePalSystemTextJson();
    if (appOptions.EnableSwagger)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c => c.AddEntityIdSchemaMap()); //强类型id swagger schema 映射
    }
    #endregion

    #region FastEndpoints

    builder.Services.AddFastEndpoints(o => { o.IncludeAbstractValidators = true; });
    builder.Services.Configure<JsonOptions>(o =>
        o.SerializerOptions.AddNetCorePalJsonConverters());
    //缓存
    builder.Services.AddResponseCaching();
    builder.Services.AddStackExchangeRedisOutputCache(options =>
    {
        options.Configuration =
            $"{redisOptions.Host}:{redisOptions.Port},password={redisOptions.Password},defaultDatabase={redisOptions.Database}";
    });
    if (appOptions.EnableSwagger)
    {
        builder.Services.SwaggerDocument(o =>
        {
            o.DocumentSettings = s =>
            {
                s.Version = "v1"; //must match what's being passed in to the map method below
                s.Title = $"{appOptions.Name} [env={displayEnv}]";
            };
            //自动Tag路径
            o.AutoTagPathSegmentIndex = 0;
        });
    }
    #endregion
    
    
    #region 公共服务

    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddSingleton<IStoreVerificationRequestHasher, StoreVerificationRequestHasher>();
    builder.Services.AddSingleton<AppleSignedDataPayloadVerifier>();
    builder.Services.AddHttpClient("apple-app-store-server-api");
    builder.Services.AddSingleton<IAppleAppStoreServerApiClient, MimoAppleAppStoreServerApiClient>();
    builder.Services.AddSingleton<MimoAppleStoreClient>();
    builder.Services.AddSingleton<IAppleStoreClient>(provider => provider.GetRequiredService<MimoAppleStoreClient>());
    builder.Services.AddSingleton<IAppleSignedPayloadVerifier>(provider => provider.GetRequiredService<MimoAppleStoreClient>());
    builder.Services.AddSingleton<IAppleStoreNotificationParser, AppleStoreNotificationParser>();
    builder.Services.AddSingleton<IGooglePlayNotificationParser, GooglePlayNotificationParser>();
    builder.Services.AddTransient<IStoreNotificationProcessor, StoreNotificationProcessor>();
    builder.Services.AddSingleton<GooglePlayServiceFactory>();
    builder.Services.AddSingleton<IGooglePlayClient, GooglePlayClient>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddRequestCancellationToken();
    builder.Services.AddRequestTimeouts();

    #endregion

    #region 模型验证器

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    builder.Services.AddKnownExceptionErrorModelInterceptor();

    #endregion
    
    
    
    #region Query

    builder.Services.AddAllQueries();

    #endregion

    
    #region RabbitMQ

    builder.Services.AddRabbitMQ(builder.Configuration.GetSection("RabbitMQ"));

    #endregion
    
    
    #region 服务注册发现

    builder.Services.AddAllContext();
    builder.Services.AddNetCorePalServiceDiscoveryClient();
    builder.Services.Configure<EnvOptions>(builder.Configuration.GetSection("Env"));

    #endregion


    #region 基础设施

    builder.Services.AddRepositories(typeof(ApplicationDbContext).Assembly);

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseMySql(builder.Configuration.GetConnectionString("MySql"),
            new MySqlServerVersion(new Version(5, 7, 30)),
            b =>
            {
                b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                b.UseMicrosoftJson();
            });
        options.LogTo(Console.WriteLine, LogLevel.Information)
            .EnableDetailedErrors();
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
        options.EnableDetailedErrors();
    });
    builder.Services.AddUnitOfWork<ApplicationDbContext>();
    builder.Services.AddRedisLocks();
    builder.Services.AddContext().AddEnvContext().AddCapContextProcessor();
    builder.Services.AddNetCorePalServiceDiscoveryClient();
    builder.Services.AddIntegrationEvents(typeof(Program))
        .UseCap<ApplicationDbContext>(b =>
        {
            b.RegisterServicesFromAssemblies(typeof(Program));
            b.AddContextIntegrationFilters();
        });


    //配置多环境Options
    builder.Services.Configure<EnvOptions>(envOptions => builder.Configuration.GetSection("Env").Bind(envOptions));
    builder.Services.AddContext().AddEnvContext().AddCapContextProcessor();
    var rabbitMqOptions = new RabbitMQOptions();
    var rabbitmqSection = builder.Configuration.GetSection("RabbitMQ");
    rabbitmqSection.Bind(rabbitMqOptions);
    builder.Services.AddCap(x =>
    {
        x.TopicNamePrefix = "bumpic";
        x.DefaultGroupName = appOptions.Name;
        x.Version = displayEnv;
        x.FailedRetryCount = 3;
        x.UseNetCorePalStorage<ApplicationDbContext>();
        x.UseRabbitMQ(p =>
        {
            p.HostName = rabbitMqOptions.HostName;
            p.UserName = rabbitMqOptions.Username;
            p.Password = rabbitMqOptions.Password;
            p.Port = rabbitMqOptions.Port;
            p.VirtualHost = rabbitMqOptions.VirtualHost;
            p.ExchangeName = "bumpic";
        });
        x.UseDashboard(options =>
        {
            options.PathMatch = $"/{appOptions.DashBoardPathPrefix}/cap";
        }); //CAP Dashboard  path：  /cap
    });

    #endregion
    
    builder.Services.AddCommandLocks(Assembly.GetExecutingAssembly());

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly())
            .AddCommandLockBehavior()
            .AddKnownExceptionValidationBehavior()
            .AddUnitOfWorkBehaviors());

    #region 多环境支持与服务注册发现

    builder.Services.AddMultiEnv(envOption => envOption.ServiceName = "Abc.Template")
        .UseMicrosoftServiceDiscovery();
    builder.Services.AddConfigurationServiceEndpointProvider();

    #endregion

    #region 远程服务客户端配置

    var jsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };
    jsonSerializerSettings.AddNetCorePalJsonConverters();
    var ser = new NewtonsoftJsonContentSerializer(jsonSerializerSettings);
    var settings = new RefitSettings(ser);
    builder.Services.AddRefitClient<IUserServiceClient>(settings)
        .ConfigureHttpClient(client =>
            client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("https+http://user:8080")!))
        .AddMultiEnvMicrosoftServiceDiscovery() //多环境服务发现支持
        .AddStandardResilienceHandler(); //添加标准的重试策略
    
    builder.Services.AddClients();
    #endregion

    #region Jobs

    builder.Services.AddJobs();
    builder.Services.AddHangfire(x =>
    {
        var hangfireRedis = builder.Configuration.GetConnectionString("hangfireRedis");
        x.UseFilter(new AutomaticRetryAttribute { Attempts = 1 });
        x.UseRedisStorage(!string.IsNullOrEmpty(hangfireRedis) ? ConnectionMultiplexer.Connect(hangfireRedis) : redis,
            new RedisStorageOptions
            {
                Prefix = string.IsNullOrEmpty(envOption.ServiceEnv)
                    ? "hangfire:Bumpic:"
                    : $"hangfire:Bumpic-{envOption.ServiceEnv}:",
                Db = !string.IsNullOrEmpty(hangfireRedis) ? 0 : redisOptions.Database
            });
    });
    builder.Services.AddHangfireServer(option =>
        option.SchedulePollingInterval = TimeSpan.FromSeconds(1)); //hangfire dashboard  path：  /hangfire

    #endregion
    
    
    #region Cros

    builder.Services.AddCors(option =>
    {
        option.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
        option.AddPolicy("imageDownload",
            policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
    });

    #endregion


    var app = builder.Build();

    // 在非生产环境中执行数据库迁移（包括开发、测试、Staging等环境）
    if (!app.Environment.IsProduction())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // 测试等场景可通过配置改为按当前模型建库（EnsureCreated），
        // 以便数据库包含全部 DbSet 对应的表而不依赖 Migration 文件。
        if (string.Equals(app.Configuration["Database:InitializeMode"], "EnsureCreated", StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        else
        {
            await dbContext.Database.MigrateAsync();
        }
    }
    else
    {
        using var scope = app.Services.CreateScope();
        if (appOptions.UseMigrateDatabase)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }

    
    app.UseForwardedHeaders();

    app.UseContext();
    app.Use(async (context, next) =>
    {
        var contextAccessor = context.RequestServices.GetRequiredService<IContextAccessor>();
        contextAccessor.SetContext(new CultureContext(Thread.CurrentThread.CurrentCulture.Name));
        await next(context);
    });

    app.UseAuthentication(); // Authentication 必须在 Authorization 之前
    app.UseKnownExceptionHandler();
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseStaticFiles();
    //app.UseHttpsRedirection();
    app.UseRouting();
    app.UseAuthorization();
    app.UseMiddleware<Bumpic.Web.Middlewares.LoginUserMiddleware>();

    app.MapControllers();
    app.UseFastEndpoints();
    
    
    if (appOptions.EnableSwagger)
    {
        app.UseSwaggerGen(
            config: c => { c.Path = $"/{appOptions.DashBoardPathPrefix}/swagger/{{documentName}}/swagger.{{json|yaml}}"; },
            uiConfig: u =>
            {
                u.Path = $"/{appOptions.DashBoardPathPrefix}/swagger";
                u.DocumentPath =
                    $"/{appOptions.DashBoardPathPrefix}/swagger/{{documentName}}/swagger.{{json|yaml}}";
            });
    }

    #region SignalR

    app.MapHub<Bumpic.Web.Application.Hubs.ChatHub>("/chat");

    #endregion

    app.UseHttpMetrics();
    app.MapHealthChecks("/health");
    app.MapMetrics(); // 通过   /metrics  访问指标
    
    // Code analysis endpoint
    app.MapGet("/code-analysis", () =>
    {
        var assemblies = new List<Assembly> { typeof(Program).Assembly, typeof(ApplicationDbContext).Assembly };
        var html = VisualizationHtmlBuilder.GenerateVisualizationHtml(
            CodeFlowAnalysisHelper.GetResultFromAssemblies(assemblies.ToArray())
        );
        return Results.Content(html, "text/html; charset=utf-8");
    });
    
    app.UseHangfireDashboard($"/{appOptions.DashBoardPathPrefix}/hangfire", new DashboardOptions()
    {
        Authorization = new[] { new HangfireNoAuthorizationFilter() }
    });
    
    if (app.Configuration.GetValue("RegisterJobs", true))
    {
        app.RegisterJobs();
    }
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

#pragma warning disable S1118
public partial class Program
#pragma warning restore S1118
{
}
