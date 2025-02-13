using System.Text;
using System.Text.Json;
using CookieStore.Contracts;
using CookieStore.Shared;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddCookieStoreInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IPaymentPublisher, RabbitMqPaymentPublisher>();

var app = builder.Build();

app.MapPost("/api/payments", (PaymentRequest payment, IPaymentPublisher publisher) =>
{
     if (payment is null)
          throw new ArgumentNullException(nameof(payment));

     publisher.Publish(payment);
     return Results.Ok(new { Status = "Payment request received" });
});

app.Run();

public interface IPaymentPublisher
{
     void Publish(PaymentRequest payment);
}

public class RabbitMqPaymentPublisher : IPaymentPublisher
{
     private readonly ConnectionFactory _factory;
     private const string QueueName = "payment_queue";

     public RabbitMqPaymentPublisher(ConnectionFactory factory)
     {
          _factory = factory ?? throw new ArgumentNullException(nameof(factory));
     }

     public void Publish(PaymentRequest payment)
     {
          if (payment is null)
               throw new ArgumentNullException(nameof(payment));
          if (string.IsNullOrWhiteSpace(payment.OrderId))
               throw new ArgumentException("OrderId cannot be null or whitespace.", nameof(payment));

          var json = JsonSerializer.Serialize(payment);
          var body = Encoding.UTF8.GetBytes(json);

          using var connection = _factory.CreateConnection();
          if (connection == null)
               throw new InvalidOperationException("Failed to create a connection to RabbitMQ.");

          using var channel = connection.CreateModel();
          if (channel == null)
               throw new InvalidOperationException("Failed to create a channel.");

          channel.QueueDeclare(
              queue: QueueName,
              durable: true,
              exclusive: false,
              autoDelete: false,
              arguments: null);

          channel.BasicPublish(
              exchange: "",
              routingKey: QueueName,
              basicProperties: null,
              body: body);
     }
}