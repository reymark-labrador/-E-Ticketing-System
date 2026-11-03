using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Checkout;
using Ticketing.Api.Data;
using Xunit;

namespace Ticketing.Tests;

public sealed class CheckoutIntegrityTests
{
    [Fact]
    public async Task Last_ticket_contention_leaves_one_winner_and_no_partial_loser_rows()
    {
        await TestDb.ResetAsync();
        await using (var setup = new TicketingDbContext(TestDb.Options()))
        {
            await setup.Database.ExecuteSqlRawAsync("""UPDATE "Tickets" SET "RemainingQuota" = 1 WHERE "Id" = 1""");
        }

        await using var dbA = new TicketingDbContext(TestDb.Options());
        await using var dbB = new TicketingDbContext(TestDb.Options());
        var checkoutA = TestDb.CreateCheckout(dbA);
        var checkoutB = TestDb.CreateCheckout(dbB);

        var taskA = checkoutA.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"race-a-{Guid.NewGuid():N}",
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        var taskB = checkoutB.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"race-b-{Guid.NewGuid():N}",
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        var results = await Task.WhenAll(
            Wrap(taskA),
            Wrap(taskB));

        Assert.Equal(1, results.Count(r => r.Order is not null));
        Assert.Equal(1, results.Count(r => r.Insufficient));

        await using var verify = new TicketingDbContext(TestDb.Options());
        Assert.Equal(0, await verify.Tickets.Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());
        Assert.Equal(1, await verify.Orders.CountAsync());
        Assert.Equal(1, await verify.Transactions.CountAsync());
        Assert.Equal(2, await verify.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Duplicate_request_id_with_different_inputs_conflicts_without_extra_reservation()
    {
        await TestDb.ResetAsync();
        await using var db = new TicketingDbContext(TestDb.Options());
        var checkout = TestDb.CreateCheckout(db);
        var requestId = $"dup-{Guid.NewGuid():N}";

        await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = requestId,
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = requestId,
            TicketId = 1,
            Quantity = 2,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None));

        Assert.Equal(99, await db.Tickets.AsNoTracking().Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());
        Assert.Equal(1, await db.Orders.CountAsync(o => o.RequestId == requestId));
    }

    [Fact]
    public async Task Replay_with_surrounding_spaces_returns_same_order_without_extra_quota_or_ledger()
    {
        await TestDb.ResetAsync();
        await using var db = new TicketingDbContext(TestDb.Options());
        var checkout = TestDb.CreateCheckout(db);
        var bareId = $"space-{Guid.NewGuid():N}";

        var first = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = bareId,
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        var replay = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"  {bareId}  ",
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(99, await db.Tickets.AsNoTracking().Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(1, await db.Transactions.CountAsync());
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.Equal(1, await db.LedgerEntries.CountAsync(e => e.Side == CheckoutConstants.LedgerSideDebit));
        Assert.Equal(1, await db.LedgerEntries.CountAsync(e => e.Side == CheckoutConstants.LedgerSideCredit));
    }

    [Fact]
    public async Task Concurrent_identical_requests_for_last_ticket_return_same_order_once()
    {
        await TestDb.ResetAsync();
        await using (var setup = new TicketingDbContext(TestDb.Options()))
        {
            await setup.Database.ExecuteSqlRawAsync("""UPDATE "Tickets" SET "RemainingQuota" = 1 WHERE "Id" = 1""");
        }

        var requestId = $"same-{Guid.NewGuid():N}";
        await using var dbA = new TicketingDbContext(TestDb.Options());
        await using var dbB = new TicketingDbContext(TestDb.Options());
        var checkoutA = TestDb.CreateCheckout(dbA);
        var checkoutB = TestDb.CreateCheckout(dbB);

        CreateOrderRequest NewRequest() => new()
        {
            RequestId = requestId,
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        };

        var results = await Task.WhenAll(
            Wrap(checkoutA.CreateAsync(NewRequest(), CancellationToken.None)),
            Wrap(checkoutB.CreateAsync(NewRequest(), CancellationToken.None)));

        Assert.Equal(2, results.Count(r => r.Order is not null));
        Assert.Equal(0, results.Count(r => r.Insufficient));
        Assert.Equal(results[0].Order!.Id, results[1].Order!.Id);

        await using var verify = new TicketingDbContext(TestDb.Options());
        Assert.Equal(0, await verify.Tickets.Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());
        Assert.Equal(1, await verify.Orders.CountAsync(o => o.RequestId == requestId));
        Assert.Equal(1, await verify.Transactions.CountAsync());
        Assert.Equal(2, await verify.LedgerEntries.CountAsync());
        Assert.Equal(1, await verify.LedgerEntries.CountAsync(e => e.Side == CheckoutConstants.LedgerSideDebit));
        Assert.Equal(1, await verify.LedgerEntries.CountAsync(e => e.Side == CheckoutConstants.LedgerSideCredit));
    }

    [Fact]
    public async Task Pending_qr_has_no_ledger_until_confirmation_and_double_confirm_is_safe()
    {
        await TestDb.ResetAsync();
        await using var db = new TicketingDbContext(TestDb.Options());
        var checkout = TestDb.CreateCheckout(db);
        var confirmation = new PaymentConfirmationService(db);

        var order = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"qr-{Guid.NewGuid():N}",
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodQr
        }, CancellationToken.None);

        Assert.Equal(CheckoutConstants.StatusPending, order.Status);
        Assert.Equal(CheckoutConstants.StatusPending, order.Payment!.Status);
        Assert.NotNull(order.Payment.DueAtUtc);
        Assert.Empty(order.Ledger);
        Assert.Equal(0, await db.LedgerEntries.CountAsync(e => e.TransactionId == order.Payment.TransactionId));
        Assert.Equal(99, await db.Tickets.AsNoTracking().Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());

        await db.Database.ExecuteSqlRawAsync(
            """UPDATE "Transactions" SET "DueAtUtc" = NOW() AT TIME ZONE 'utc' - INTERVAL '1 second' WHERE "Id" = {0}""",
            order.Payment.TransactionId);

        var first = await confirmation.TryConfirmAsync(order.Payment.TransactionId, CancellationToken.None);
        var second = await confirmation.TryConfirmAsync(order.Payment.TransactionId, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);

        db.ChangeTracker.Clear();
        var paid = await checkout.GetAsync(order.Id, CancellationToken.None);
        Assert.Equal(CheckoutConstants.StatusSucceeded, paid!.Status);
        Assert.Equal(CheckoutConstants.StatusSucceeded, paid.Payment!.Status);
        Assert.Equal(2, paid.Ledger.Count);
        Assert.Contains(paid.Ledger, e => e is { Account: CheckoutConstants.AccountCashGateway, Side: CheckoutConstants.LedgerSideDebit, AmountAed: 100m });
        Assert.Contains(paid.Ledger, e => e is { Account: CheckoutConstants.AccountTicketSalesRevenue, Side: CheckoutConstants.LedgerSideCredit, AmountAed: 100m });
        Assert.Equal(2, await db.LedgerEntries.CountAsync(e => e.TransactionId == order.Payment.TransactionId));
        Assert.Equal(99, await db.Tickets.AsNoTracking().Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());
    }

    [Fact]
    public async Task Ledger_balance_assertions_and_empty_ledger_sum_to_zero()
    {
        await TestDb.ResetAsync();
        await using var db = new TicketingDbContext(TestDb.Options());
        var checkout = TestDb.CreateCheckout(db);

        var emptyDebits = await db.LedgerEntries.Where(e => e.Side == CheckoutConstants.LedgerSideDebit).SumAsync(e => (decimal?)e.AmountAed) ?? 0m;
        var emptyCredits = await db.LedgerEntries.Where(e => e.Side == CheckoutConstants.LedgerSideCredit).SumAsync(e => (decimal?)e.AmountAed) ?? 0m;
        Assert.Equal(0m, emptyDebits - emptyCredits);

        var order = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"bal-{Guid.NewGuid():N}",
            TicketId = 2,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        Assert.Equal(2, order.Ledger.Count);
        Assert.Equal(order.Ledger.Where(e => e.Side == CheckoutConstants.LedgerSideDebit).Sum(e => e.AmountAed),
            order.Ledger.Where(e => e.Side == CheckoutConstants.LedgerSideCredit).Sum(e => e.AmountAed));
        Assert.All(order.Ledger, e => Assert.Equal(200m, e.AmountAed));
        Assert.Contains(order.Ledger, e => e.Account == CheckoutConstants.AccountCashGateway && e.Side == CheckoutConstants.LedgerSideDebit);
        Assert.Contains(order.Ledger, e => e.Account == CheckoutConstants.AccountTicketSalesRevenue && e.Side == CheckoutConstants.LedgerSideCredit);
    }

    private static async Task<(OrderResponse? Order, bool Insufficient)> Wrap(Task<OrderResponse> task)
    {
        try
        {
            return (await task, false);
        }
        catch (InsufficientQuotaException)
        {
            return (null, true);
        }
    }
}
