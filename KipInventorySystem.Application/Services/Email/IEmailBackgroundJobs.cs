namespace KipInventorySystem.Application.Services.Email
{
    public interface IEmailBackgroundJobs
    {
        Task SendLowStockAlertEmailAsync(string toEmail, string recipientName, string warehouseName, string warehouseCode, string productName, string sku, int availableQuantity, int threshold, int reorderQuantity, CancellationToken cancellationToken = default);
        Task SendManualProcurementReviewEmailAsync(string toEmail, string recipientName, string warehouseName, string warehouseCode, string productName, string sku, int availableQuantity, int threshold, CancellationToken cancellationToken = default);
        Task SendPasswordChangedEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default);
        Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetLink, int expirationHours, CancellationToken cancellationToken = default);
        Task SendPasswordResetSuccessEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default);
        Task SendPurchaseOrderApprovedEmailAsync(string toEmail, string supplierName, string purchaseOrderNumber, string warehouseName, DateTime? expectedArrivalDate, string lineSummary, string? notes, CancellationToken cancellationToken = default);
        Task SendVerificationEmailAsync(string toEmail, string firstName, string verifyLink, CancellationToken cancellationToken = default);

        Task SendResendVerificationEmailAsync(
        string toEmail,
        string firstName,
        string verifyLink,
        CancellationToken cancellationToken = default);
    }
}