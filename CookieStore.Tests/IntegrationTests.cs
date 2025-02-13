using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;

namespace CookieStore.Tests;

[TestClass]
public class CookieStoreIntegrationTests
{
    private const string ConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
    private const string PaymentQueue = "payment_queue";
    private const string ProcessedExchange = "payment_processed_exchange";

    private ConnectionFactory CreateFactory() =>
        new ConnectionFactory { Uri = new Uri(ConnectionString) };

    [TestInitialize]
    public void Setup()
    {
        using var connection = CreateFactory().CreateConnection();
        using var channel = connection.CreateModel();
        EnsureQueueDeclared(channel, PaymentQueue);
        channel.QueuePurge(PaymentQueue);
    }

    private void EnsureQueueDeclared(IModel channel, string queueName)
    {
        channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    [TestMethod]
    public void RabbitMqPaymentPublisher_ShouldPublishMessageToQueue()
    {
        // Arrange
        var paymentRequest = new PaymentRequest("order123", 99.99m);
        var factory = CreateFactory();
        var publisher = new RabbitMqPaymentPublisher(factory);

        // Act
        publisher.Publish(paymentRequest);

        // Assert
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        EnsureQueueDeclared(channel, PaymentQueue);

        var result = channel.BasicGet(PaymentQueue, autoAck: true);
        Assert.IsNotNull(result, "Expected a message in the payment_queue.");

        var message = Encoding.UTF8.GetString(result.Body.ToArray());
        var deserialized = JsonSerializer.Deserialize<PaymentRequest>(message);
        Assert.IsNotNull(deserialized, "Failed to deserialize PaymentRequest.");
        Assert.AreEqual(paymentRequest.OrderId, deserialized.OrderId);
        Assert.AreEqual(paymentRequest.Amount, deserialized.Amount);
    }

    [TestMethod]
    public void PaymentProcessor_ShouldProcessPayment()
    {
        // Arrange: Publish a payment request.
        var paymentRequest = new PaymentRequest("order456", 49.99m);
        var factory = CreateFactory();
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            EnsureQueueDeclared(channel, PaymentQueue);
            var json = JsonSerializer.Serialize(paymentRequest);
            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(
                exchange: "",
                routingKey: PaymentQueue,
                basicProperties: null,
                body: body);
        }

        // Arrange: Set up a temporary queue to capture the processed event.
        using var tempConnection = factory.CreateConnection();
        using var tempChannel = tempConnection.CreateModel();
        tempChannel.ExchangeDeclare(
            exchange: ProcessedExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);

        var tempQueueName = tempChannel.QueueDeclare(
            queue: "",       // broker-generated name
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null).QueueName;
        tempChannel.QueueBind(
            queue: tempQueueName,
            exchange: ProcessedExchange,
            routingKey: "");

        // Act: Process the payment.
        var processor = new PaymentProcessor(ConnectionString);
        processor.Start();
        Thread.Sleep(2000); // Allow processing time.
        processor.Stop();

        // Assert: Verify that a PaymentProcessed message was published.
        var result = tempChannel.BasicGet(tempQueueName, autoAck: true);
        Assert.IsNotNull(result, "Expected a PaymentProcessed message in the temporary queue.");

        var message = Encoding.UTF8.GetString(result.Body.ToArray());
        var processed = JsonSerializer.Deserialize<PaymentProcessed>(message);
        Assert.IsNotNull(processed, "Failed to deserialize PaymentProcessed.");
        Assert.AreEqual(paymentRequest.OrderId, processed.OrderId);
        Assert.AreEqual("Processed", processed.Status);
    }
}