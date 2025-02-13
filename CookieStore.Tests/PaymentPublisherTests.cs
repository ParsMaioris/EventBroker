using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;

namespace CookieStore.Tests;

[TestClass]
public class PaymentPublisherTests
{
    private const string ConnectionString = "amqps://pdfnvtxf:bnpGPG4SYTEYSLDmF7XTcBrS7rhK28TD@gull.rmq.cloudamqp.com/pdfnvtxf";
    private const string PaymentQueue = "payment_queue";

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

        // Assert: Confirm message published to the payment queue.
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
}