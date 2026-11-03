using Ticketing.Api.Checkout;
using Ticketing.Api.Data;
using Xunit;

namespace Ticketing.Tests;

public sealed class OrderListTests
{
    [Fact]
    public async Task List_returns_latest_orders_newest_first_with_ticket_type()
    {
        await TestDb.ResetAsync();
        await using var db = new TicketingDbContext(TestDb.Options());
        var checkout = TestDb.CreateCheckout(db);

        var older = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"list-older-{Guid.NewGuid():N}",
            TicketId = 1,
            Quantity = 1,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        await Task.Delay(20);

        var newer = await checkout.CreateAsync(new CreateOrderRequest
        {
            RequestId = $"list-newer-{Guid.NewGuid():N}",
            TicketId = 2,
            Quantity = 2,
            PaymentMethod = CheckoutConstants.PaymentMethodCard
        }, CancellationToken.None);

        var listed = await checkout.ListAsync(CancellationToken.None);

        Assert.Equal(2, listed.Count);
        Assert.Equal(newer.Id, listed[0].Id);
        Assert.Equal("Premium", listed[0].TicketType);
        Assert.Equal(2, listed[0].Quantity);
        Assert.Equal(400m, listed[0].TotalAed);
        Assert.Equal(CheckoutConstants.PaymentMethodCard, listed[0].PaymentMethod);
        Assert.Equal(CheckoutConstants.StatusSucceeded, listed[0].Status);
        Assert.Equal(older.Id, listed[1].Id);
        Assert.Equal("Gold", listed[1].TicketType);
    }
}
