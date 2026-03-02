namespace KipInventorySystem.Application.Services.Payment.DTOs;

public record CreateCustomerDTO
{
    public string Email { get; init; } = string.Empty;
    public string? Name { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
