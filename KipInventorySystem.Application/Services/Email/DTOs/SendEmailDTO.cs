namespace KipInventorySystem.Application.Services.Email.DTOs;

public record SendEmailDTO
{
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsHtml { get; init; } = false;
}
