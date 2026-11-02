using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Checkout;
using Ticketing.Api.Data;

namespace Ticketing.Tests;

internal static class TestDb
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("TICKETING_TEST_DB")
        ?? "Host=localhost;Port=55432;Database=ticketing_tests;Username=ticketing;Password=local_ticketing";

    public static DbContextOptions<TicketingDbContext> Options() =>
        new DbContextOptionsBuilder<TicketingDbContext>().UseNpgsql(ConnectionString).Options;

    public static async Task ResetAsync()
    {
        await using var db = new TicketingDbContext(Options());
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE "LedgerEntries", "Transactions", "Orders" CASCADE""");
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Tickets" SET "RemainingQuota" = "Capacity" """);
    }

    public static CheckoutService CreateCheckout(TicketingDbContext db)
    {
        var confirmation = new PaymentConfirmationService(db);
        IPaymentHandler[] handlers = [new CreditCardHandler(), new QRHandler()];
        return new CheckoutService(db, handlers, confirmation);
    }
}
