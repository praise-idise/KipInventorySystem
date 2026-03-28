using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Email;

public interface IEmailBackgroundJobs
{
    Task SendWelcomeEmailAsync(string toEmail, string firstName, string lastName, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetLink, int expirationHours, CancellationToken cancellationToken = default);
    Task SendPasswordChangedEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default);
    Task SendPasswordResetSuccessEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default);
    Task SendPurchaseOrderApprovedEmailAsync(
        string toEmail,
        string supplierName,
        string purchaseOrderNumber,
        string warehouseName,
        DateTime? expectedArrivalDate,
        string lineSummary,
        string? notes,
        CancellationToken cancellationToken = default);
}

public class EmailBackgroundJobs(IEmailService emailService, ILogger<EmailBackgroundJobs> logger) : IEmailBackgroundJobs
{
    public async Task SendWelcomeEmailAsync(string toEmail, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        try
        {
            var welcomeEmailHtml = EmailTemplates.WelcomeEmail(firstName, lastName);
            await emailService.SendHtmlEmailAsync(
                toEmail,
                "Welcome to Our Platform!",
                welcomeEmailHtml,
                cancellationToken);
            
            logger.LogInformation("Welcome email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetLink, int expirationHours, CancellationToken cancellationToken = default)
    {
        try
        {
            var passwordResetEmailHtml = EmailTemplates.PasswordResetEmail(firstName, resetLink, expirationHours);
            await emailService.SendHtmlEmailAsync(
                toEmail,
                "Password Reset Request",
                passwordResetEmailHtml,
                cancellationToken);
            
            logger.LogInformation("Password reset email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendPasswordChangedEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            var passwordChangedEmailHtml = EmailTemplates.PasswordChangedEmail(firstName, ipAddress, userAgent);
            await emailService.SendHtmlEmailAsync(
                toEmail,
                "Your Password Has Been Changed",
                passwordChangedEmailHtml,
                cancellationToken);
            
            logger.LogInformation("Password changed notification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password changed email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendPasswordResetSuccessEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            var passwordResetSuccessEmailHtml = EmailTemplates.PasswordResetSuccessEmail(firstName, ipAddress, userAgent);
            await emailService.SendHtmlEmailAsync(
                toEmail,
                "Your Password Has Been Reset",
                passwordResetSuccessEmailHtml,
                cancellationToken);
            
            logger.LogInformation("Password reset success email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset success email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendPurchaseOrderApprovedEmailAsync(
        string toEmail,
        string supplierName,
        string purchaseOrderNumber,
        string warehouseName,
        DateTime? expectedArrivalDate,
        string lineSummary,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var emailHtml = EmailTemplates.PurchaseOrderApprovedEmail(
                supplierName,
                purchaseOrderNumber,
                warehouseName,
                expectedArrivalDate,
                lineSummary,
                notes);

            await emailService.SendHtmlEmailAsync(
                toEmail,
                $"Purchase Order {purchaseOrderNumber} Approved",
                emailHtml,
                cancellationToken);

            logger.LogInformation("Purchase order approval email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send purchase order approval email to {Email}", toEmail);
            throw;
        }
    }
}
