namespace Ticketing.Api.Checkout;

public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public required string Account { get; set; }
    public required string Side { get; set; }
    public decimal AmountAed { get; set; }

    public PaymentTransaction? Transaction { get; set; }
}
