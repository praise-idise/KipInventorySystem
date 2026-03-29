namespace KipInventorySystem.Application.Services.Email;

public static class EmailTemplates
{
    public static string WelcomeEmail(string firstName, string lastName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4F46E5; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #4F46E5; color: white; text-decoration: none; border-radius: 6px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to Our Platform!</h1>
        </div>
        <div class='content'>
            <h2>Hello {firstName} {lastName},</h2>
            <p>Thank you for registering with us! We're excited to have you on board.</p>
            <p>Your account has been successfully created and you can now access all the features of our platform.</p>
            <p>If you have any questions or need assistance, please don't hesitate to reach out to our support team.</p>
            <p>Best regards,<br>The Team</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string PasswordResetEmail(string firstName, string resetLink, int expirationHours = 24)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4F46E5; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #4F46E5; color: white; text-decoration: none; border-radius: 6px; margin: 20px 0; }}
        .warning {{ background-color: #FEF3C7; border-left: 4px solid #F59E0B; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Password Reset Request</h1>
        </div>
        <div class='content'>
            <h2>Hello {firstName},</h2>
            <p>We received a request to reset your password. Click the button below to create a new password:</p>
            <div style='text-align: center;'>
                <a href='{resetLink}' class='button'>Reset Password</a>
            </div>
            <p>Or copy and paste this link into your browser:</p>
            <p style='word-break: break-all; color: #4F46E5;'>{resetLink}</p>
            <div class='warning'>
                <strong>⚠️ Important:</strong> This link will expire in {expirationHours} hours.
            </div>
            <p>If you didn't request a password reset, please ignore this email or contact support if you have concerns.</p>
            <p>Best regards,<br>The Team</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string PasswordChangedEmail(string firstName, string ipAddress, string userAgent)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #10B981; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background-color: #DBEAFE; border-left: 4px solid #3B82F6; padding: 15px; margin: 20px 0; }}
        .warning {{ background-color: #FEE2E2; border-left: 4px solid #EF4444; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✓ Password Changed Successfully</h1>
        </div>
        <div class='content'>
            <h2>Hello {firstName},</h2>
            <p>Your password has been successfully changed.</p>
            <div class='info-box'>
                <strong>Change Details:</strong><br>
                Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC<br>
                IP Address: {ipAddress}<br>
                Device: {userAgent}
            </div>
            <div class='warning'>
                <strong>⚠️ Didn't make this change?</strong><br>
                If you didn't change your password, please contact our support team immediately and secure your account.
            </div>
            <p>All your active sessions have been logged out for security. Please log in again with your new password.</p>
            <p>Best regards,<br>The Team</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string PasswordResetSuccessEmail(string firstName, string ipAddress, string userAgent)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #10B981; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background-color: #DBEAFE; border-left: 4px solid #3B82F6; padding: 15px; margin: 20px 0; }}
        .warning {{ background-color: #FEE2E2; border-left: 4px solid #EF4444; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✓ Password Reset Complete</h1>
        </div>
        <div class='content'>
            <h2>Hello {firstName},</h2>
            <p>Your password has been successfully reset.</p>
            <div class='info-box'>
                <strong>Reset Details:</strong><br>
                Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC<br>
                IP Address: {ipAddress}<br>
                Device: {userAgent}
            </div>
            <div class='warning'>
                <strong>⚠️ Didn't make this change?</strong><br>
                If you didn't reset your password, please contact our support team immediately. Your account may be compromised.
            </div>
            <p>All your active sessions have been logged out for security. You can now log in with your new password.</p>
            <p>Best regards,<br>The Team</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string LowStockAlertEmail(
        string recipientName,
        string warehouseName,
        string warehouseCode,
        string productName,
        string sku,
        int availableQuantity,
        int threshold,
        int reorderQuantity)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #B45309; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background-color: #FEF3C7; border-left: 4px solid #B45309; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Low Stock Alert</h1>
        </div>
        <div class='content'>
            <h2>Hello {recipientName},</h2>
            <p>A product has reached its low-stock threshold and may need replenishment.</p>
            <div class='info-box'>
                <strong>Warehouse:</strong> {warehouseName} ({warehouseCode})<br>
                <strong>Product:</strong> {productName}<br>
                <strong>SKU:</strong> {sku}<br>
                <strong>Available Quantity:</strong> {availableQuantity}<br>
                <strong>Threshold:</strong> {threshold}<br>
                <strong>Suggested Reorder Quantity:</strong> {reorderQuantity}
            </div>
            <p>Please review current stock levels and the generated procurement work for this item.</p>
            <p>Best regards,<br>The Inventory Team</p>
        </div>
        <div class='footer'>
            <p>&copy; {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string ManualProcurementReviewEmail(
        string recipientName,
        string warehouseName,
        string warehouseCode,
        string productName,
        string sku,
        int availableQuantity,
        int threshold)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #991B1B; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background-color: #FEE2E2; border-left: 4px solid #DC2626; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Manual Procurement Review Needed</h1>
        </div>
        <div class='content'>
            <h2>Hello {recipientName},</h2>
            <p>A low-stock product could not be prepared for auto-reorder because it does not have a default supplier configured.</p>
            <div class='info-box'>
                <strong>Warehouse:</strong> {warehouseName} ({warehouseCode})<br>
                <strong>Product:</strong> {productName}<br>
                <strong>SKU:</strong> {sku}<br>
                <strong>Available Quantity:</strong> {availableQuantity}<br>
                <strong>Threshold:</strong> {threshold}
            </div>
            <p>Please assign a supplier or create a purchase order manually.</p>
            <p>Best regards,<br>The Inventory Team</p>
        </div>
        <div class='footer'>
            <p>&copy; {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string PurchaseOrderApprovedEmail(
        string supplierName,
        string purchaseOrderNumber,
        string warehouseName,
        DateTime? expectedArrivalDate,
        string lineSummary,
        string? notes)
    {
        var expectedArrivalText = expectedArrivalDate.HasValue
            ? expectedArrivalDate.Value.ToString("yyyy-MM-dd")
            : "Not specified";

        var notesText = string.IsNullOrWhiteSpace(notes)
            ? "No additional notes were provided."
            : notes.Trim();

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0F766E; color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
        .info-box {{ background-color: #ECFEFF; border-left: 4px solid #0F766E; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6B7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Purchase Order Approved</h1>
        </div>
        <div class='content'>
            <h2>Hello {supplierName},</h2>
            <p>A purchase order has been approved and is ready for your attention.</p>
            <div class='info-box'>
                <strong>Purchase Order:</strong> {purchaseOrderNumber}<br>
                <strong>Warehouse:</strong> {warehouseName}<br>
                <strong>Expected Arrival:</strong> {expectedArrivalText}
            </div>
            <p><strong>Order Summary:</strong><br>{lineSummary}</p>
            <p><strong>Notes:</strong><br>{notesText}</p>
            <p>Please contact us if you need any clarification.</p>
            <p>Best regards,<br>The Inventory Team</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }
}
