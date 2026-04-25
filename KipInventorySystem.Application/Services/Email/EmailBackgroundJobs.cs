using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Email;

public class EmailBackgroundJobs(IEmailService emailService, ILogger<EmailBackgroundJobs> logger) : IEmailBackgroundJobs
{
    public async Task SendVerificationEmailAsync(
    string toEmail,
    string firstName,
    string verifyLink,
    CancellationToken cancellationToken = default)
    {
        try
        {
            var html = EmailTemplates.VerificationEmail(firstName, verifyLink);
            await emailService.SendHtmlEmailAsync(
                toEmail,
                "Verify your email address",
                html,
                cancellationToken);

            logger.LogInformation("Verification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendResendVerificationEmailAsync(
    string toEmail,
    string firstName,
    string verifyLink,
    CancellationToken cancellationToken = default)
    {
        try
        {
            var html = EmailTemplates.ResendVerificationEmail(firstName, verifyLink);
            await emailService.SendHtmlEmailAsync(
                toEmail,
                "New Email Verification Link",
                html,
                cancellationToken);

            logger.LogInformation("Resend verification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send resend verification email to {Email}", toEmail);
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

    public async Task SendLowStockAlertEmailAsync(
        string toEmail,
        string recipientName,
        string warehouseName,
        string warehouseCode,
        string productName,
        string sku,
        int availableQuantity,
        int threshold,
        int reorderQuantity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var emailHtml = EmailTemplates.LowStockAlertEmail(
                recipientName,
                warehouseName,
                warehouseCode,
                productName,
                sku,
                availableQuantity,
                threshold,
                reorderQuantity);

            await emailService.SendHtmlEmailAsync(
                toEmail,
                $"Low Stock Alert: {productName} ({sku})",
                emailHtml,
                cancellationToken);

            logger.LogInformation("Low stock alert email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send low stock alert email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendManualProcurementReviewEmailAsync(
        string toEmail,
        string recipientName,
        string warehouseName,
        string warehouseCode,
        string productName,
        string sku,
        int availableQuantity,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var emailHtml = EmailTemplates.ManualProcurementReviewEmail(
                recipientName,
                warehouseName,
                warehouseCode,
                productName,
                sku,
                availableQuantity,
                threshold);

            await emailService.SendHtmlEmailAsync(
                toEmail,
                $"Manual Procurement Review Needed: {productName} ({sku})",
                emailHtml,
                cancellationToken);

            logger.LogInformation("Manual procurement review email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send manual procurement review email to {Email}", toEmail);
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
