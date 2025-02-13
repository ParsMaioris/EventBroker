using CookieStore.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CookieStore.Tests;

[TestClass]
public abstract class TestBase
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    [TestInitialize]
    public void SetupDI()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RabbitMq:Scheme", "amqps" },
                { "RabbitMq:Host", "gull.rmq.cloudamqp.com" },
                { "RabbitMq:Port", "5671" },
                { "RabbitMq:Username", "inMemoryTestUsername" },
                { "RabbitMq:Password", "inMemoryTestPassword" },
                { "RabbitMq:VirtualHost", "inMemoryTestVhost" }
            })
            .AddUserSecrets<TestBase>(optional: true)
            .Build();

        services.AddCookieStoreInfrastructure(configuration);

        ServiceProvider = services.BuildServiceProvider();
    }
}