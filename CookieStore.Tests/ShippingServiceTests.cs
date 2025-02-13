using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;

namespace CookieStore.Tests;

[TestClass]
public class ShippingServiceTests
{
    private const string ConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
    private const string ShippingQueue = "shipping_queue";
    private const string ProcessedExchange = "payment_processed_exchange";

    private ConnectionFactory CreateFactory() =>
        new ConnectionFactory { Uri = new Uri(ConnectionString) };

    [TestInitialize]
    public void Setup()
    {
        using var connection = CreateFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueueDeclare(
            queue: ShippingQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueuePurge(ShippingQueue);

        channel.ExchangeDeclare(
            exchange: ProcessedExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(
            queue: ShippingQueue,
            exchange: ProcessedExchange,
            routingKey: "");
    }

    [TestMethod]
    public void ShippingService_ShouldProcessPaymentProcessedMessage()
    {
        // Arrange: Create a PaymentProcessed message.
        var processedMessage = new PaymentProcessed("orderShip", "Processed");
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

        // Act: Start the ShippingService.
        var shippingService = new ShippingService(ConnectionString);
        shippingService.Start();

        // Poll until the message is consumed.
        bool messageConsumed = false;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000)
        {
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var result = channel.BasicGet(ShippingQueue, autoAck: true);
                if (result == null)
                {
                    messageConsumed = true;
                    break;
                }
            }
            Thread.Sleep(200);
        }
        shippingService.Stop();

        // Assert: Verify the shipping queue is empty.
        Assert.IsTrue(messageConsumed, "Expected the ShippingService to process and consume the PaymentProcessed message.");
    }
}