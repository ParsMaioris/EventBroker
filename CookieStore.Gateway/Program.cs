using CookieStore.Contracts;
using CookieStore.Gateway;
using CookieStore.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddCookieStoreInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IPaymentPublisher, PaymentPublisher>();

var app = builder.Build();

app.MapPost("/api/payments", (PaymentRequest payment, IPaymentPublisher publisher) =>
{
     if (payment is null)
          throw new ArgumentNullException(nameof(payment));

     publisher.Publish(payment);
     return Results.Ok(new { Status = "Payment request received" });
});

app.Run();