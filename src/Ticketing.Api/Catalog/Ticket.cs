namespace Ticketing.Api.Catalog;

public sealed class Ticket
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal PriceAed { get; set; }
    public int Capacity { get; set; }
    public int RemainingQuota { get; set; }
}
