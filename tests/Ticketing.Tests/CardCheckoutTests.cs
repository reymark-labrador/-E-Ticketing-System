using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Checkout;
using Ticketing.Api.Data;
using Xunit;

namespace Ticketing.Tests;

public sealed class CardCheckoutTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("TICKETING_TEST_DB")
        ?? "Host=localhost;Port=55432;Database=ticketing_tests;Username=ticketing;Password=local_ticketing";

    [Fact]
    public async Task Card_purchase_of_two_gold_tickets_posts_balanced_ledger_and_reduces_quota()
    {
        var options = new DbContextOptionsBuilder<TicketingDbContext>().UseNpgsql(ConnectionString).Options;
        await using (var setup = new TicketingDbContext(options))
        {
            await setup.Database.MigrateAsync();
            await setup.Database.ExecuteSqlRawAsync("""TRUNCATE "LedgerEntries", "Transactions", "Orders" RESTART IDENTITY CASCADE""");
            await setup.Database.ExecuteSqlRawAsync("""UPDATE "Tickets" SET "RemainingQuota" = "Capacity" """);
        }

        await using var db = new TicketingDbContext(options);
        var checkout = new CheckoutService(db);
        var requestId = $"card-gold-{Guid.NewGuid():N}";

        var order = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = requestId,
            TicketId = 1,
            Quantity = 2,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        Assert.Equal(200m, order.TotalAed);
        Assert.Equal(100m, order.UnitPriceAed);
        Assert.Equal(CheckoutConstants.StatusSucceeded, order.Status);
        Assert.NotNull(order.Payment);
        Assert.Equal(CheckoutConstants.StatusSucceeded, order.Payment!.Status);
        Assert.Equal(200m, order.Payment.AmountAed);
        Assert.NotNull(order.Payment.ConfirmedAtUtc);
        Assert.Equal(2, order.Ledger.Count);
        Assert.Contains(order.Ledger, e => e is { Account: CheckoutConstants.AccountCashGateway, Side: CheckoutConstants.LedgerSideDebit, AmountAed: 200m });
        Assert.Contains(order.Ledger, e => e is { Account: CheckoutConstants.AccountTicketSalesRevenue, Side: CheckoutConstants.LedgerSideCredit, AmountAed: 200m });

        var gold = await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == 1);
        Assert.Equal(98, gold.RemainingQuota);

        var replay = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = requestId,
            TicketId = 1,
            Quantity = 2,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        Assert.Equal(order.Id, replay.Id);
        Assert.Equal(98, await db.Tickets.AsNoTracking().Where(t => t.Id == 1).Select(t => t.RemainingQuota).SingleAsync());
        Assert.Equal(1, await db.Orders.CountAsync(o => o.RequestId == requestId));
        Assert.Equal(2, await db.LedgerEntries.CountAsync(e => e.TransactionId == order.Payment.TransactionId));
    }
}
