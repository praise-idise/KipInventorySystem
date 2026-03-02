using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Services.Email.DTOs;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.Infrastructure.Integrations.Email;

internal class EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger) : IEmailService
{
    private readonly SmtpSettings _smtpSettings = smtpSettings.Value;

    public async Task SendEmailAsync(SendEmailDTO emailDto, CancellationToken cancellationToken)
    {
        if (emailDto.IsHtml)
        {
            await SendHtmlEmailAsync(emailDto.ToEmail, emailDto.Subject, emailDto.Body, cancellationToken);
        }
        else
        {
            await SendEmailAsync(emailDto.ToEmail, emailDto.Subject, emailDto.Body, cancellationToken);
        }
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        await SendEmailInternalAsync(toEmail, subject, body, false, cancellationToken);
    }

    public async Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        await SendEmailInternalAsync(toEmail, subject, htmlBody, true, cancellationToken);
    }

    private async Task SendEmailInternalAsync(string toEmail, string subject, string body, bool isHtml, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            message.Body = new TextPart(isHtml ? "html" : "plain")
            {
                Text = body
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, SecureSocketOptions.SslOnConnect, cancellationToken);
            await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Email sent successfully to {EmailAddress}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {EmailAddress}. Subject: {Subject}", toEmail, subject);
            throw;
        }
    }
}
