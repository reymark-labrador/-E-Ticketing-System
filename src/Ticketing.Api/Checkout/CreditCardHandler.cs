namespace Ticketing.Api.Checkout;

public sealed class CreditCardHandler : IPaymentHandler
{
    public string Method => CheckoutConstants.PaymentMethodCard;

    public void Prepare(Order order, PaymentTransaction payment, DateTimeOffset now, PaymentConfirmationService confirmation)
    {
        payment.PaymentMethod = Method;
        confirmation.AttachSucceededPayment(order, payment, now);
    }
}
