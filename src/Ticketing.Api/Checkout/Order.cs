namespace Ticketing.Api.Checkout;

public sealed class Order
{
    public Guid Id { get; set; }
    public required string RequestId { get; set; }
    public int TicketId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceAed { get; set; }
    public decimal TotalAed { get; set; }
    public required string PaymentMethod { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public PaymentTransaction? Transaction { get; set; }
}
