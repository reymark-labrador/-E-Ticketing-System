namespace Ticketing.Api.Checkout;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public required string PaymentMethod { get; set; }
    public required string Status { get; set; }
    public decimal AmountAed { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Order? Order { get; set; }
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}
