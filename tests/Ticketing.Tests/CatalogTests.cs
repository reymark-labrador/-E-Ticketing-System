using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Xunit;

namespace Ticketing.Tests;

public sealed class CatalogTests
{
    [Fact]
    public async Task Migration_seeds_the_three_priced_ticket_types()
    {
        var connection = Environment.GetEnvironmentVariable("TICKETING_TEST_DB")
            ?? "Host=localhost;Port=55432;Database=ticketing_tests;Username=ticketing;Password=local_ticketing";
        var options = new DbContextOptionsBuilder<TicketingDbContext>().UseNpgsql(connection).Options;
        await using var db = new TicketingDbContext(options);
        await db.Database.MigrateAsync();

        var tickets = await db.Tickets.OrderBy(t => t.Id).ToListAsync();

        Assert.Equal(new[] { "Gold", "Premium", "VIP" }, tickets.Select(t => t.Name).ToArray());
        Assert.Equal(new[] { 100m, 200m, 500m }, tickets.Select(t => t.PriceAed).ToArray());
        Assert.Equal(100, tickets[0].Capacity);
        Assert.All(tickets, t => Assert.InRange(t.RemainingQuota, 0, t.Capacity));
    }
}
