namespace CookieStore.Contracts;

public record PaymentRequest(string OrderId, decimal Amount);
public record PaymentProcessed(string OrderId, string Status);