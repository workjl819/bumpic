namespace __PROJECT_NAME__.Web.Options;

public class AppOptions
{
    public bool UseEnvContext { get; set; } = false;
    public string Name { get; set; } = "photo-rescue";
    public bool UseConsul { get; set; } = false;
    /// <summary>
    /// 是否启用 Swagger 文档和界面。
    /// </summary>
    public bool EnableSwagger { get; set; } = true;
    public bool UseGithub { get; set; } = false;
    public bool UseHttpsRedirection { get; set; } = true;
    public bool UseMigrateDatabase { get; set; } = false;
    public bool UseDevTools { get; set; } = false;
    
    public string ProductTenant { get; set; } = "photo-rescue";
    
    public string DashBoardPathPrefix { get; set; } = "devops";
    public string PathPrefix { get; set; } = "photo-rescue";
    public string ServiceNamespace { get; set; } = "app-photo-rescue";

    public bool UseProxyProtocol { get; set; } = false;
    
    public string CurrentEnvironment { get; set; } = "dev";
    
}
