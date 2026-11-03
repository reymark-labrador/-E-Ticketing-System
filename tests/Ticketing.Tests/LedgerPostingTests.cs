using Ticketing.Api.Checkout;
using Xunit;

namespace Ticketing.Tests;

public class LedgerPostingTests
{
    [Fact]
    public void Aed_200_payment_produces_exactly_two_balanced_entries_for_same_transaction()
    {
        var transactionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var entries = PaymentConfirmationService.CreateBalancedLedgerEntries(transactionId, 200m);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e =>
        {
            Assert.Equal(transactionId, e.TransactionId);
            Assert.Equal(200m, e.AmountAed);
        });
        Assert.Contains(entries, e =>
            e.Account == CheckoutConstants.AccountCashGateway
            && e.Side == CheckoutConstants.LedgerSideDebit
            && e.AmountAed == 200m);
        Assert.Contains(entries, e =>
            e.Account == CheckoutConstants.AccountTicketSalesRevenue
            && e.Side == CheckoutConstants.LedgerSideCredit
            && e.AmountAed == 200m);
    }
}
