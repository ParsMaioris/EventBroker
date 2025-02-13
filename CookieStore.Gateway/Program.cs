using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string rabbitMqConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
var factory = new ConnectionFactory() { Uri = new Uri(rabbitMqConnectionString) };

app.MapPost("/api/payments", (PaymentRequest payment) =>
{
    var json = JsonSerializer.Serialize(payment);
    var body = Encoding.UTF8.GetBytes(json);

    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();
    channel.QueueDeclare(
         queue: "payment_queue",
         durable: true,
         exclusive: false,
         autoDelete: false,
         arguments: null);

    channel.BasicPublish(
         exchange: "",
         routingKey: "payment_queue",
         basicProperties: null,
         body: body);

    return Results.Ok(new { Status = "Payment request received" });
});

app.Run();

public record PaymentRequest(string OrderId, decimal Amount);