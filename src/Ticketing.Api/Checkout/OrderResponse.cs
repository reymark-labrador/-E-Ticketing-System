namespace Ticketing.Api.Checkout;

public sealed class OrderResponse
{
    public Guid Id { get; init; }
    public string RequestId { get; init; } = "";
    public int TicketId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPriceAed { get; init; }
    public decimal TotalAed { get; init; }
    public string PaymentMethod { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public PaymentResponse? Payment { get; init; }
    public IReadOnlyList<LedgerEntryResponse> Ledger { get; init; } = [];

    public static OrderResponse From(Order order)
    {
        var payment = order.Transaction;
        return new OrderResponse
        {
            Id = order.Id,
            RequestId = order.RequestId,
            TicketId = order.TicketId,
            Quantity = order.Quantity,
            UnitPriceAed = order.UnitPriceAed,
            TotalAed = order.TotalAed,
            PaymentMethod = order.PaymentMethod,
            Status = order.Status,
            CreatedAtUtc = order.CreatedAtUtc,
            Payment = payment is null
                ? null
                : new PaymentResponse
                {
                    TransactionId = payment.Id,
                    Status = payment.Status,
                    AmountAed = payment.AmountAed,
                    DueAtUtc = payment.DueAtUtc,
                    ConfirmedAtUtc = payment.ConfirmedAtUtc
                },
            Ledger = payment?.LedgerEntries
                .OrderBy(e => e.Side)
                .Select(e => new LedgerEntryResponse
                {
                    Account = e.Account,
                    Side = e.Side,
                    AmountAed = e.AmountAed
                })
                .ToList() ?? []
        };
    }
}

public sealed class PaymentResponse
{
    public Guid TransactionId { get; init; }
    public string Status { get; init; } = "";
    public decimal AmountAed { get; init; }
    public DateTimeOffset? DueAtUtc { get; init; }
    public DateTimeOffset? ConfirmedAtUtc { get; init; }
}

public sealed class LedgerEntryResponse
{
    public string Account { get; init; } = "";
    public string Side { get; init; } = "";
    public decimal AmountAed { get; init; }
}
