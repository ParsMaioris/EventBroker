namespace CookieStore.Notifications;

public record NotificationEvent(string OrderId, bool EmailSent, bool SmsSent);