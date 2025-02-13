using CookieStore.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

var rabbitMqConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
var notificationService = new NotificationService(rabbitMqConnectionString);
notificationService.Start();

Console.WriteLine("Press [enter] to exit.");
Console.ReadLine();

notificationService.Stop();

public class NotificationService
{
    private readonly ConnectionFactory _factory;
    private IConnection _connection = null!;
    private IModel _channel = null!;
    private const string ExchangeName = "payment_processed_exchange";
    private const string QueueName = "notification_queue";

    public NotificationService(string rabbitMqConnectionString)
    {
        if (string.IsNullOrEmpty(rabbitMqConnectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(rabbitMqConnectionString));

        _factory = new ConnectionFactory() { Uri = new Uri(rabbitMqConnectionString) };
    }

    public void Start()
    {
        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();

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

        Console.WriteLine(" [*] Waiting for processed payment events (Notifications)...");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += OnNotificationReceived;
        _channel.BasicConsume(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);
    }

    private void OnNotificationReceived(object? sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine($" [x] Notification service received: {message}");

        var processed = JsonSerializer.Deserialize<PaymentProcessed>(message)
                        ?? throw new InvalidOperationException("Failed to deserialize PaymentProcessed.");

        Console.WriteLine($" [x] Sending notification for OrderId: {processed.OrderId}");

        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }

    public void Stop()
    {
        if (_channel == null || _connection == null)
            throw new InvalidOperationException("NotificationService is not running.");

        _channel.Close();
        _connection.Close();
    }
}