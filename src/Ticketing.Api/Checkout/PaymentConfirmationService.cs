using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;

namespace Ticketing.Api.Checkout;

public sealed class PaymentConfirmationService(TicketingDbContext db)
{
    public void AttachSucceededPayment(Order order, PaymentTransaction payment, DateTimeOffset confirmedAt)
    {
        order.Status = CheckoutConstants.StatusSucceeded;
        payment.Status = CheckoutConstants.StatusSucceeded;
        payment.ConfirmedAtUtc = confirmedAt;
        payment.DueAtUtc = null;

        payment.LedgerEntries = AddBalancedLedgerEntries(payment);
    }

    public async Task<int> ConfirmDuePaymentsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var dueIds = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.Status == CheckoutConstants.StatusPending
                && t.DueAtUtc != null
                && t.DueAtUtc <= now)
            .OrderBy(t => t.DueAtUtc)
            .Select(t => t.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var confirmed = 0;
        foreach (var id in dueIds)
        {
            if (await TryConfirmAsync(id, cancellationToken))
                confirmed++;
        }

        return confirmed;
    }

    public async Task<bool> TryConfirmAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var claimed = await db.Transactions
            .Where(t =>
                t.Id == transactionId
                && t.Status == CheckoutConstants.StatusPending
                && t.DueAtUtc != null
                && t.DueAtUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.Status, CheckoutConstants.StatusSucceeded)
                    .SetProperty(t => t.ConfirmedAtUtc, now),
                cancellationToken);

        if (claimed != 1)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        var payment = await db.Transactions
            .AsNoTracking()
            .SingleAsync(t => t.Id == transactionId, cancellationToken);

        await db.Orders
            .Where(o => o.Id == payment.OrderId && o.Status == CheckoutConstants.StatusPending)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(o => o.Status, CheckoutConstants.StatusSucceeded),
                cancellationToken);

        AddBalancedLedgerEntries(payment);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private List<LedgerEntry> AddBalancedLedgerEntries(PaymentTransaction payment)
    {
        var debit = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = payment.Id,
            Account = CheckoutConstants.AccountCashGateway,
            Side = CheckoutConstants.LedgerSideDebit,
            AmountAed = payment.AmountAed
        };

        var credit = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = payment.Id,
            Account = CheckoutConstants.AccountTicketSalesRevenue,
            Side = CheckoutConstants.LedgerSideCredit,
            AmountAed = payment.AmountAed
        };

        db.LedgerEntries.Add(debit);
        db.LedgerEntries.Add(credit);
        return [debit, credit];
    }
}
