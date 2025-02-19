using CookieStore.Payments;
using CookieStore.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddUserSecrets<Program>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddCookieStoreInfrastructure(context.Configuration);
        services.AddTransient<PaymentProcessor>();
    })
    .Build();

var processor = host.Services.GetRequiredService<PaymentProcessor>();
processor.Start();

Console.WriteLine("Press [enter] to exit.");
Console.ReadLine();

processor.Stop();