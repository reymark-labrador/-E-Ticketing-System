using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;

namespace Ticketing.Api.Checkout;

public sealed class CheckoutService(TicketingDbContext db)
{
    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
            throw new CheckoutValidationException("RequestId is required.");
        if (request.Quantity < 1)
            throw new CheckoutValidationException("Quantity must be a positive integer.");
        if (request.PaymentMethod is not (CheckoutConstants.PaymentMethodCard or CheckoutConstants.PaymentMethodQr))
            throw new CheckoutValidationException("PaymentMethod must be Card or QR.");

        // Milestone 1 ships card checkout only; QR lands in milestone 2.
        if (request.PaymentMethod != CheckoutConstants.PaymentMethodCard)
            throw new CheckoutValidationException("Only Card payment is supported in this milestone.");

        var existing = await db.Orders
            .Include(o => o.Transaction)!
            .ThenInclude(t => t!.LedgerEntries)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.RequestId == request.RequestId, cancellationToken);

        if (existing is not null)
        {
            if (existing.TicketId != request.TicketId
                || existing.Quantity != request.Quantity
                || !string.Equals(existing.PaymentMethod, request.PaymentMethod, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException();
            }

            return OrderResponse.From(existing);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // Re-check under the transaction in case of a race on RequestId.
        existing = await db.Orders
            .Include(o => o.Transaction)!
            .ThenInclude(t => t!.LedgerEntries)
            .FirstOrDefaultAsync(o => o.RequestId == request.RequestId, cancellationToken);

        if (existing is not null)
        {
            await tx.CommitAsync(cancellationToken);
            if (existing.TicketId != request.TicketId
                || existing.Quantity != request.Quantity
                || !string.Equals(existing.PaymentMethod, request.PaymentMethod, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException();
            }

            return OrderResponse.From(existing);
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
            throw new InsufficientQuotaException();

        var now = DateTimeOffset.UtcNow;
        var total = decimal.Round(ticket.PriceAed * request.Quantity, 2, MidpointRounding.AwayFromZero);
        var orderId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var order = new Order
        {
            Id = orderId,
            RequestId = request.RequestId.Trim(),
            TicketId = ticket.Id,
            Quantity = request.Quantity,
            UnitPriceAed = ticket.PriceAed,
            TotalAed = total,
            PaymentMethod = CheckoutConstants.PaymentMethodCard,
            Status = CheckoutConstants.StatusSucceeded,
            CreatedAtUtc = now
        };

        var payment = new PaymentTransaction
        {
            Id = transactionId,
            OrderId = orderId,
            PaymentMethod = CheckoutConstants.PaymentMethodCard,
            Status = CheckoutConstants.StatusSucceeded,
            AmountAed = total,
            ConfirmedAtUtc = now,
            CreatedAtUtc = now
        };

        var debit = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            Account = CheckoutConstants.AccountCashGateway,
            Side = CheckoutConstants.LedgerSideDebit,
            AmountAed = total
        };

        var credit = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            Account = CheckoutConstants.AccountTicketSalesRevenue,
            Side = CheckoutConstants.LedgerSideCredit,
            AmountAed = total
        };

        db.Orders.Add(order);
        db.Transactions.Add(payment);
        db.LedgerEntries.AddRange(debit, credit);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            var replay = await db.Orders
                .Include(o => o.Transaction)!
                .ThenInclude(t => t!.LedgerEntries)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.RequestId == request.RequestId, cancellationToken);

            if (replay is null)
                throw;

            if (replay.TicketId != request.TicketId
                || replay.Quantity != request.Quantity
                || !string.Equals(replay.PaymentMethod, request.PaymentMethod, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException();
            }

            return OrderResponse.From(replay);
        }

        order.Transaction = payment;
        payment.LedgerEntries = [debit, credit];
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
}
