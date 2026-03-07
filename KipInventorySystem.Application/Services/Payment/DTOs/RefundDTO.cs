using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record RefundDTO
{
    [DefaultValue("pi_3N0samplePaymentIntent")]
    public string PaymentIntentId { get; init; } = string.Empty;

    [DefaultValue(50000)]
    public long? Amount { get; init; }

    [DefaultValue("requested_by_customer")]
    public string? Reason { get; init; }
}
