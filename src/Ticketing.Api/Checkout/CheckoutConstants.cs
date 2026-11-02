namespace Ticketing.Api.Checkout;

public static class CheckoutConstants
{
    public const string PaymentMethodCard = "Card";
    public const string PaymentMethodQr = "QR";

    public const string StatusPending = "Pending";
    public const string StatusSucceeded = "Succeeded";

    public const string LedgerSideDebit = "Debit";
    public const string LedgerSideCredit = "Credit";

    public const string AccountCashGateway = "Cash/Payment Gateway";
    public const string AccountTicketSalesRevenue = "Ticket Sales Revenue";
}
