namespace CookieStore.Notifications;

public class NotificationEventStore
{
    private readonly Dictionary<string, NotificationEvent> _store = new();

    public NotificationEvent GetOrCreate(string orderId)
    {
        if (!_store.TryGetValue(orderId, out var evt))
        {
            evt = new NotificationEvent(orderId, false, false);
            _store[orderId] = evt;
        }
        return evt;
    }

    public void Update(NotificationEvent evt) => _store[evt.OrderId] = evt;
}