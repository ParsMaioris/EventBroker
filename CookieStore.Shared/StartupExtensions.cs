using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CookieStore.Shared;

public static class StartupExtensions
{
    public static IServiceCollection AddCookieStoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var uri = new UriBuilder
            {
                Scheme = options.Scheme,
                Host = options.Host,
                Port = options.Port,
                UserName = options.Username,
                Password = options.Password,
                Path = options.VirtualHost
            }.Uri;
            return new ConnectionFactory { Uri = uri };
        });

        return services;
    }
}