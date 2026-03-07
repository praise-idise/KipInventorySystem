using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record CreatePaymentIntentDTO
{
    [DefaultValue(250000)]
    public long Amount { get; init; }

    [DefaultValue("usd")]
    public string Currency { get; init; } = "usd";

    [DefaultValue("Inventory order payment")]
    public string? Description { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }
}
