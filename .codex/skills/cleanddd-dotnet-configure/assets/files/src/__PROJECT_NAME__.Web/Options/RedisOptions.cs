namespace __PROJECT_NAME__.Web.Options;

public class RedisOptions
{
    public int Database { get; set; }
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string Password { get; set; } = null!;
}