using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

string rabbitMqConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
var shippingService = new ShippingService(rabbitMqConnectionString);
shippingService.Start();

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();

shippingService.Stop();

public class ShippingService
{
    private readonly ConnectionFactory _factory;
    private IConnection _connection = null!;
    private IModel _channel = null!;
    private const string ExchangeName = "payment_processed_exchange";
    private const string QueueName = "shipping_queue";

    public ShippingService(string rabbitMqConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(rabbitMqConnectionString));

        _factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
    }

    public void Start()
    {
        _connection = _factory.CreateConnection() ?? throw new InvalidOperationException("Failed to create RabbitMQ connection.");
        _channel = _connection.CreateModel() ?? throw new InvalidOperationException("Failed to create RabbitMQ channel.");

        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.QueueBind(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "");

        Console.WriteLine(" [*] Waiting for processed payment events (Shipping)...");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += OnShippingReceived;
        _channel.BasicConsume(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);
    }

    private void OnShippingReceived(object? sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine($" [x] Shipping service received: {message}");

        var processed = JsonSerializer.Deserialize<PaymentProcessed>(message)
                        ?? throw new InvalidOperationException("Failed to deserialize PaymentProcessed message.");

        Console.WriteLine($" [x] Processing shipping for OrderId: {processed.OrderId}");
        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }

    public void Stop()
    {
        if (_channel == null || _connection == null)
            throw new InvalidOperationException("ShippingService is not running.");

        _channel.Close();
        _connection.Close();
    }
}

public record PaymentProcessed(string OrderId, string Status);