using System.Text;
using System.Text.Json;
using CookieStore.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CookieStore.Notifications;

public class NotificationService
{
    private readonly ConnectionFactory _factory;
    private IConnection _connection = null!;
    private IModel _channel = null!;
    private const string ExchangeName = "payment_processed_exchange";
    private const string QueueName = "notification_queue";
    protected readonly NotificationEventStore _eventStore = new();

    public NotificationService(string rabbitMqConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
            throw new ArgumentException("Invalid connection string", nameof(rabbitMqConnectionString));
        _factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
    }

    public void Start()
    {
        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(QueueName, ExchangeName, routingKey: "");
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += OnNotificationReceived;
        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
    }

    private void OnNotificationReceived(object? sender, BasicDeliverEventArgs ea)
    {
        var message = Encoding.UTF8.GetString(ea.Body.ToArray());
        var processed = JsonSerializer.Deserialize<PaymentProcessed>(message)
                        ?? throw new InvalidOperationException("Failed to deserialize PaymentProcessed.");
        var notification = _eventStore.GetOrCreate(processed.OrderId);

        try
        {
            notification = ProcessEmail(notification, processed);
            notification = ProcessSms(notification, processed);
            _eventStore.Update(notification);

            if (notification.EmailSent && notification.SmsSent)
                _channel.BasicAck(ea.DeliveryTag, false);
            else
                _channel.BasicNack(ea.DeliveryTag, false, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during processing: {ex.Message}");
            _channel.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private NotificationEvent ProcessEmail(NotificationEvent notification, PaymentProcessed processed)
    {
        if (!notification.EmailSent)
        {
            try { notification = notification with { EmailSent = true }; }
            catch { }
        }
        return notification;
    }

    protected virtual NotificationEvent ProcessSms(NotificationEvent notification, PaymentProcessed processed)
    {
        if (!notification.SmsSent)
        {
            try { notification = notification with { SmsSent = true }; }
            catch { }
        }
        return notification;
    }

    public void Stop()
    {
        _channel.Close();
        _connection.Close();
    }
}