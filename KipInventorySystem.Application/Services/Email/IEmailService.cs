using KipInventorySystem.Application.Services.Email.DTOs;

namespace KipInventorySystem.Application.Services.Email;

public interface IEmailService
{
    Task SendEmailAsync(SendEmailDTO emailDto, CancellationToken cancellationToken);
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);
    Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken);
}
