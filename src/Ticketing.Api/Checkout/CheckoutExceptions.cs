namespace Ticketing.Api.Checkout;

public sealed class CheckoutValidationException(string message) : Exception(message);

public sealed class InsufficientQuotaException() : Exception("Insufficient ticket availability.");

public sealed class IdempotencyConflictException() : Exception("RequestId was already used with different inputs.");
