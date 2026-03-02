namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record RefundDTO
{
    public string PaymentIntentId { get; init; } = string.Empty;
    public long? Amount { get; init; }
    public string? Reason { get; init; }
}
