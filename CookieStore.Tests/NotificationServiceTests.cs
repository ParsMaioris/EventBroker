using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;
using Microsoft.Extensions.DependencyInjection;
using CookieStore.Notifications;

namespace CookieStore.Tests;

[TestCategory("Integration")]
[TestClass]
public class NotificationServiceTests : TestBase
{
    private const string NotificationQueue = "notification_queue";
    private const string ProcessedExchange = "payment_processed_exchange";

    private ConnectionFactory CreateFactory() =>
       ServiceProvider.GetRequiredService<ConnectionFactory>();

    [TestInitialize]
    public void Setup()
    {
        using var connection = CreateFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(
            queue: NotificationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueuePurge(NotificationQueue);

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
    }

    [TestMethod]
    public void NotificationService_ShouldProcessPaymentProcessedMessage()
    {
        // Arrange: Create a PaymentProcessed message.
        var processedMessage = new PaymentProcessed("orderNotif", "Processed");
        var processedJson = JsonSerializer.Serialize(processedMessage);
        var processedBody = Encoding.UTF8.GetBytes(processedJson);

        var factory = CreateFactory();
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.BasicPublish(
                exchange: ProcessedExchange,
                routingKey: "",
                basicProperties: null,
                body: processedBody);
        }

        // Act: Start the NotificationService.
        var connectionString = ServiceProvider.GetRequiredService<ConnectionFactory>().Uri.ToString();
        var notificationService = new NotificationService(connectionString);
        notificationService.Start();

        // Wait (with polling) for the message to be consumed.
        bool messageConsumed = false;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000)
        {
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var result = channel.BasicGet(NotificationQueue, autoAck: true);
                if (result == null)
                {
                    messageConsumed = true;
                    break;
                }
            }
            Thread.Sleep(200);
        }
        notificationService.Stop();

        // Assert: Verify the queue is empty.
        Assert.IsTrue(messageConsumed, "Expected the NotificationService to process and consume the PaymentProcessed message.");
    }
}