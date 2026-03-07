using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record CreateCustomerDTO
{
    [DefaultValue("buyer@kipinventory.com")]
    public string Email { get; init; } = string.Empty;

    [DefaultValue("Kip Buyer One")]
    public string? Name { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }
}
