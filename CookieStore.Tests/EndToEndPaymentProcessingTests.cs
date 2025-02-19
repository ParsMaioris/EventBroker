using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;
using Microsoft.Extensions.DependencyInjection;
using CookieStore.Shipping;
using CookieStore.Payments;
using CookieStore.Notifications;

namespace CookieStore.Tests;

[TestClass]
public class EndToEndPaymentProcessingTests : TestBase
{
    private const string PaymentQueue = "payment_queue";
    private const string NotificationQueue = "notification_queue";
    private const string ShippingQueue = "shipping_queue";
    private const string ProcessedExchange = "payment_processed_exchange";

    private ConnectionFactory CreateFactory() =>
        ServiceProvider.GetRequiredService<ConnectionFactory>();

    [TestInitialize]
    public void Setup()
    {
        using var connection = CreateFactory().CreateConnection();
        using var channel = connection.CreateModel();

        // Payment queue.
        channel.QueueDeclare(
            queue: PaymentQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueuePurge(PaymentQueue);

        // Notification queue.
        channel.QueueDeclare(
            queue: NotificationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueuePurge(NotificationQueue);

        // Shipping queue.
        channel.QueueDeclare(
            queue: ShippingQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueuePurge(ShippingQueue);

        // Exchange.
        channel.ExchangeDeclare(
            exchange: ProcessedExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(
            queue: NotificationQueue,
            exchange: ProcessedExchange,
            routingKey: "");
        channel.QueueBind(
            queue: ShippingQueue,
            exchange: ProcessedExchange,
            routingKey: "");
    }

    [TestMethod]
    public void EndToEndPaymentProcessing_ShouldRouteAndProcessMessages()
    {
        // Publish a PaymentRequest message to the payment queue.
        var paymentRequest = new PaymentRequest("orderE2E", 150.00m);
        var factory = CreateFactory();
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var json = JsonSerializer.Serialize(paymentRequest);
            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(
                exchange: "",
                routingKey: PaymentQueue,
                basicProperties: null,
                body: body);
        }

        // Act: Start PaymentProcessor, NotificationService, and ShippingService.
        var connectionString = ServiceProvider.GetRequiredService<ConnectionFactory>().Uri.ToString();
        var processor = new PaymentProcessor(connectionString);
        var notificationService = new NotificationService(connectionString);
        var shippingService = new ShippingService(connectionString);

        processor.Start();
        notificationService.Start();
        shippingService.Start();

        // Allow time for full end-to-end processing.
        Thread.Sleep(5000);

        processor.Stop();
        notificationService.Stop();
        shippingService.Stop();

        // Assert: Verify that all queues are empty.
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var paymentResult = channel.BasicGet(PaymentQueue, autoAck: true);
            var notificationResult = channel.BasicGet(NotificationQueue, autoAck: true);
            var shippingResult = channel.BasicGet(ShippingQueue, autoAck: true);

            Assert.IsNull(paymentResult, "Expected payment_queue to be empty after processing.");
            Assert.IsNull(notificationResult, "Expected notification_queue to be empty after processing.");
            Assert.IsNull(shippingResult, "Expected shipping_queue to be empty after processing.");
        }
    }
}