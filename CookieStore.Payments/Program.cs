using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

string rabbitMqConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
var factory = new ConnectionFactory() { Uri = new Uri(rabbitMqConnectionString) };

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(
    queue: "payment_queue",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

channel.ExchangeDeclare(
    exchange: "payment_processed_exchange",
    type: ExchangeType.Fanout,
    durable: true,
    autoDelete: false,
    arguments: null);

Console.WriteLine(" [*] Waiting for payment messages...");

var consumer = new EventingBasicConsumer(channel);
consumer.Received += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    Console.WriteLine($" [x] Received: {message}");

    var payment = JsonSerializer.Deserialize<PaymentRequest>(message);
    Console.WriteLine($" [x] Processing payment for OrderId: {payment?.OrderId}, Amount: {payment?.Amount}");
    Thread.Sleep(1000);

    var processed = new PaymentProcessed(payment?.OrderId, "Processed");
    var processedJson = JsonSerializer.Serialize(processed);
    var processedBody = Encoding.UTF8.GetBytes(processedJson);

    channel.BasicPublish(
         exchange: "payment_processed_exchange",
         routingKey: "",
         basicProperties: null,
         body: processedBody);

    Console.WriteLine($" [x] Published processed event for OrderId: {payment?.OrderId}");
    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
};

channel.BasicConsume(
    queue: "payment_queue",
    autoAck: false,
    consumer: consumer);

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();

public record PaymentRequest(string OrderId, decimal Amount);
public record PaymentProcessed(string OrderId, string Status);