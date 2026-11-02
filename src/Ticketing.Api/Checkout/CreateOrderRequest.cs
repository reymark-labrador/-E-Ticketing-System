using System.ComponentModel.DataAnnotations;

namespace Ticketing.Api.Checkout;

public sealed class CreateOrderRequest
{
    [Required]
    [MaxLength(100)]
    public string RequestId { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int TicketId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be a positive integer.")]
    public int Quantity { get; set; }

    [Required]
    [RegularExpression("^(Card|QR)$", ErrorMessage = "PaymentMethod must be Card or QR.")]
    public string PaymentMethod { get; set; } = "";
}
