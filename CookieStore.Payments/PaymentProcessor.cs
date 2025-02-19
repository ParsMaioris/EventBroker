using System.Text;
using System.Text.Json;
using CookieStore.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CookieStore.Payments;

public class PaymentProcessor
{
    private readonly ConnectionFactory _factory;
    private IConnection _connection = null!;
    private IModel _channel = null!;
    private const string PaymentQueue = "payment_queue";
    private const string ProcessedExchange = "payment_processed_exchange";

    public PaymentProcessor(string rabbitMqConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(rabbitMqConnectionString));

        _factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
    }

    public void Start()
    {
        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: PaymentQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.ExchangeDeclare(
            exchange: ProcessedExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);

        Console.WriteLine(" [*] Waiting for payment messages...");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += OnPaymentReceived;
        _channel.BasicConsume(
            queue: PaymentQueue,
            autoAck: false,
            consumer: consumer);
    }

    private void OnPaymentReceived(object? sender, BasicDeliverEventArgs ea)
    {
        if (_channel == null)
            throw new InvalidOperationException("Channel is not initialized.");

        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine($" [x] Received: {message}");

        var payment = JsonSerializer.Deserialize<PaymentRequest>(message)
                      ?? throw new InvalidOperationException("Failed to deserialize PaymentRequest.");

        Console.WriteLine($" [x] Processing payment for OrderId: {payment.OrderId}, Amount: {payment.Amount}");

        Thread.Sleep(1000);

        var processed = new PaymentProcessed(payment.OrderId, "Processed");
        var processedJson = JsonSerializer.Serialize(processed);
        var processedBody = Encoding.UTF8.GetBytes(processedJson);

        _channel.BasicPublish(
            exchange: ProcessedExchange,
            routingKey: "",
            basicProperties: null,
            body: processedBody);

        Console.WriteLine($" [x] Published processed event for OrderId: {payment.OrderId}");
        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }

    public void Stop()
    {
        if (_channel == null || _connection == null)
            throw new InvalidOperationException("PaymentProcessor is not running.");

        _channel.Close();
        _connection.Close();
    }
}