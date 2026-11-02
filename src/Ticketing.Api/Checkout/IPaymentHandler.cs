namespace Ticketing.Api.Checkout;

public interface IPaymentHandler
{
    string Method { get; }

    void Prepare(Order order, PaymentTransaction payment, DateTimeOffset now, PaymentConfirmationService confirmation);
}
