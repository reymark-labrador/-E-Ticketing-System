namespace Ticketing.Api.Checkout;

public sealed class OrderSummaryResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string TicketType { get; init; } = "";
    public int Quantity { get; init; }
    public decimal TotalAed { get; init; }
    public string PaymentMethod { get; init; } = "";
    public string Status { get; init; } = "";
}
