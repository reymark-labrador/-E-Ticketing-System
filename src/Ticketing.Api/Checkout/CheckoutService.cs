using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;

namespace Ticketing.Api.Checkout;

public sealed class CheckoutService(
    TicketingDbContext db,
    IEnumerable<IPaymentHandler> paymentHandlers,
    PaymentConfirmationService confirmation)
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
            throw new CheckoutValidationException("RequestId is required.");
        if (request.Quantity < 1)
            throw new CheckoutValidationException("Quantity must be a positive integer.");

        var requestId = request.RequestId.Trim();

        var handler = paymentHandlers.FirstOrDefault(h =>
            string.Equals(h.Method, request.PaymentMethod, StringComparison.Ordinal));
        if (handler is null)
            throw new CheckoutValidationException("PaymentMethod must be Card or QR.");

        var existing = await LoadByRequestIdAsync(requestId, tracking: false, cancellationToken);
        if (existing is not null)
            return ReplayOrConflict(existing, request);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        existing = await LoadByRequestIdAsync(requestId, tracking: true, cancellationToken);
        if (existing is not null)
        {
            await tx.CommitAsync(cancellationToken);
            return ReplayOrConflict(existing, request);
        }

        var ticket = await db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new CheckoutValidationException($"Ticket {request.TicketId} was not found.");

        var reserved = await db.Tickets
            .Where(t => t.Id == request.TicketId && t.RemainingQuota >= request.Quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.RemainingQuota, t => t.RemainingQuota - request.Quantity),
                cancellationToken);

        if (reserved != 1)
        {
            // Same requestId may have won the quota race; row lock means that order is committed by now.
            var raced = await LoadByRequestIdAsync(requestId, tracking: false, cancellationToken);
            if (raced is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                return ReplayOrConflict(raced, request);
            }

            await tx.RollbackAsync(cancellationToken);
            throw new InsufficientQuotaException();
        }

        var now = DateTimeOffset.UtcNow;
        var total = decimal.Round(ticket.PriceAed * request.Quantity, 2, MidpointRounding.AwayFromZero);
        var orderId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var order = new Order
        {
            Id = orderId,
            RequestId = requestId,
            TicketId = ticket.Id,
            Quantity = request.Quantity,
            UnitPriceAed = ticket.PriceAed,
            TotalAed = total,
            PaymentMethod = handler.Method,
            Status = CheckoutConstants.StatusPending,
            CreatedAtUtc = now
        };

        var payment = new PaymentTransaction
        {
            Id = transactionId,
            OrderId = orderId,
            PaymentMethod = handler.Method,
            Status = CheckoutConstants.StatusPending,
            AmountAed = total,
            CreatedAtUtc = now
        };

        db.Orders.Add(order);
        db.Transactions.Add(payment);
        handler.Prepare(order, payment, now, confirmation);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            var replay = await LoadByRequestIdAsync(requestId, tracking: false, cancellationToken);
            if (replay is null)
                throw;

            return ReplayOrConflict(replay, request);
        }

        order.Transaction = payment;
        return OrderResponse.From(order);
    }

    public async Task<OrderResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Transaction)!
            .ThenInclude(t => t!.LedgerEntries)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : OrderResponse.From(order);
    }

    public async Task<IReadOnlyList<OrderSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await (
            from o in db.Orders.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on o.TicketId equals t.Id
            orderby o.CreatedAtUtc descending
            select new OrderSummaryResponse
            {
                Id = o.Id,
                CreatedAtUtc = o.CreatedAtUtc,
                TicketType = t.Name,
                Quantity = o.Quantity,
                TotalAed = o.TotalAed,
                PaymentMethod = o.PaymentMethod,
                Status = o.Status
            })
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    private async Task<Order?> LoadByRequestIdAsync(string requestId, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Order> query = db.Orders
            .Include(o => o.Transaction)!
            .ThenInclude(t => t!.LedgerEntries);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(o => o.RequestId == requestId, cancellationToken);
    }

    private static OrderResponse ReplayOrConflict(Order existing, CreateOrderRequest request)
    {
        if (existing.TicketId != request.TicketId
            || existing.Quantity != request.Quantity
            || !string.Equals(existing.PaymentMethod, request.PaymentMethod, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }

        return OrderResponse.From(existing);
    }
}
