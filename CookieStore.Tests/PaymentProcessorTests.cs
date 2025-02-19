using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;
using Microsoft.Extensions.DependencyInjection;
using CookieStore.Payments;

namespace CookieStore.Tests;

[TestClass]
public class PaymentProcessorTests : TestBase
{
    private const string PaymentQueue = "payment_queue";
    private const string ProcessedExchange = "payment_processed_exchange";

    private ConnectionFactory CreateFactory() =>
       ServiceProvider.GetRequiredService<ConnectionFactory>();

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
        var connectionString = ServiceProvider.GetRequiredService<ConnectionFactory>().Uri.ToString();
        var processor = new PaymentProcessor(connectionString);
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