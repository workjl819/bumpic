namespace __PROJECT_NAME__.Web.Options;

public class RabbitMQOptions
{
    public string HostName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public int Port { get; set; }
    public string VirtualHost { get; set; } = null!;
}