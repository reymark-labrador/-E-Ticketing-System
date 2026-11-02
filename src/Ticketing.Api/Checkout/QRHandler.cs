namespace Ticketing.Api.Checkout;

public sealed class QRHandler : IPaymentHandler
{
    public string Method => CheckoutConstants.PaymentMethodQr;

    public void Prepare(Order order, PaymentTransaction payment, DateTimeOffset now, PaymentConfirmationService confirmation)
    {
        order.Status = CheckoutConstants.StatusPending;
        payment.PaymentMethod = Method;
        payment.Status = CheckoutConstants.StatusPending;
        payment.DueAtUtc = now.Add(CheckoutConstants.QrDueDelay);
        payment.ConfirmedAtUtc = null;
    }
}
