namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record PaymentIntentResponseDTO
{
    public string Id { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public long Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
