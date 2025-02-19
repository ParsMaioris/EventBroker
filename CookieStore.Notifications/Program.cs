using CookieStore.Notifications;
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
        services.AddTransient<NotificationService>();
    })
    .Build();

var notificationService = host.Services.GetRequiredService<NotificationService>();
notificationService.Start();

Console.WriteLine("Press [enter] to exit.");
Console.ReadLine();

notificationService.Stop();