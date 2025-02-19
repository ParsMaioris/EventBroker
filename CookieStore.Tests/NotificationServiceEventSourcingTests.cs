using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using CookieStore.Contracts;
using CookieStore.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace CookieStore.Tests
{
    public class TestNotificationService : NotificationService
    {
        private bool _smsFailed = false;
        public int SmsAttemptCount { get; private set; } = 0;
        public TestNotificationService(string connectionString) : base(connectionString) { }

        protected override NotificationEvent ProcessSms(NotificationEvent notification, PaymentProcessed processed)
        {
            SmsAttemptCount++;
            if (!_smsFailed)
            {
                _smsFailed = true;
                throw new Exception("Simulated SMS failure");
            }
            return base.ProcessSms(notification, processed);
        }

        public NotificationEvent GetEvent(string orderId) => _eventStore.GetOrCreate(orderId);
    }

    [TestClass]
    public class NotificationServiceEventSourcingTests : TestBase
    {
        private const string NotificationQueue = "notification_queue";
        private const string ProcessedExchange = "payment_processed_exchange";
        private const string ShippingQueue = "shipping_queue";

        private ConnectionFactory CreateFactory() =>
            ServiceProvider.GetRequiredService<ConnectionFactory>();

        [TestInitialize]
        public void Setup()
        {
            using var connection = CreateFactory().CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: NotificationQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueuePurge(NotificationQueue);

            channel.QueueDeclare(queue: ShippingQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueuePurge(ShippingQueue);

            channel.ExchangeDeclare(exchange: ProcessedExchange, type: ExchangeType.Fanout, durable: true, autoDelete: false, arguments: null);
            channel.QueueBind(queue: NotificationQueue, exchange: ProcessedExchange, routingKey: "");
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var connection = CreateFactory().CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueuePurge(ShippingQueue);
        }

        [TestMethod]
        public void EventSourcing_RetriesFailedNotification()
        {
            var processed = new PaymentProcessed("order-test-event", "Processed");
            var factory = CreateFactory();
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var json = JsonSerializer.Serialize(processed);
                var body = Encoding.UTF8.GetBytes(json);
                channel.BasicPublish(exchange: ProcessedExchange, routingKey: "", basicProperties: null, body: body);
            }

            var connectionString = CreateFactory().Uri.ToString();
            var service = new TestNotificationService(connectionString);
            service.Start();
            Thread.Sleep(5000);
            service.Stop();

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var notificationResult = channel.BasicGet(NotificationQueue, autoAck: true);
                Assert.IsNull(notificationResult, "Expected notification_queue to be empty after processing.");
            }

            var evt = service.GetEvent("order-test-event");
            Assert.IsTrue(evt.EmailSent, "Email notification should be marked as sent.");
            Assert.IsTrue(evt.SmsSent, "SMS notification should be marked as sent after retry.");
            Assert.IsTrue(service.SmsAttemptCount >= 2, "SMS processing should have been attempted at least twice.");
        }
    }
}