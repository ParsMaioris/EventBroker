namespace CookieStore.Shared;

public class RabbitMqOptions
{
    public string Scheme { get; set; } = "amqps";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 5671;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string VirtualHost { get; set; } = "/";
}