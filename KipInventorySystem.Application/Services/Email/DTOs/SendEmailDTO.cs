using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Email.DTOs;

public record SendEmailDTO
{
    [DefaultValue("operations@kipinventory.com")]
    public string ToEmail { get; init; } = string.Empty;

    [DefaultValue("Low Stock Alert")]
    public string Subject { get; init; } = string.Empty;

    [DefaultValue("Product SKU-IPHONE15-BLK-128 has reached reorder threshold.")]
    public string Body { get; init; } = string.Empty;

    [DefaultValue(false)]
    public bool IsHtml { get; init; } = false;
}
