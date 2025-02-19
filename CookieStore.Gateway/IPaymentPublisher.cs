using CookieStore.Contracts;

namespace CookieStore.Gateway;

public interface IPaymentPublisher
{
    void Publish(PaymentRequest payment);
}