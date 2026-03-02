namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record CreatePaymentIntentDTO
{
    public long Amount { get; init; }
    public string Currency { get; init; } = "usd";
    public string? Description { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
