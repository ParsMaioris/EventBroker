using CookieStore.Shared;
using CookieStore.Shipping;
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
        services.AddTransient<ShippingService>();
    })
    .Build();

var shippingService = host.Services.GetRequiredService<ShippingService>();
shippingService.Start();

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();

shippingService.Stop();