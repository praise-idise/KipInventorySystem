namespace KipInventorySystem.Shared.Models;

public static class AppSettings
{
    public class JwtSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; }
        public int RefreshTokenDays { get; set; }
    }

    public class FrontendSettings
    {
        // Base URL of the frontend, e.g. https://app.example.com
        public string BaseUrl { get; set; } = string.Empty;

        // Path used for reset password flow (no leading slash), default is "reset-password"
        public string ResetPasswordPath { get; set; } = "reset-password";
    }

    public class AdminSettings
    {
        public string Username { get; set; } = "AdminUser";
        public string Password { get; set; } = "AdminUser123$";
        public string Email { get; set; } = "admin@example.com";
        public string FirstName { get; set; } = "Admin";
        public string LastName { get; set; } = "User";
    }

    public class CloudinarySettings
    {
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public string FolderPrefix { get; set; } = "MyApp";
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }

    public class StripeSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string PublishableKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
    }

    public class HangfireSettings
    {
        public string DashboardUsername { get; set; } = "Admin";
        public string DashboardPassword { get; set; } = "HangfireAdmin123&";
    }

    public class CorsSettings
    {
        public string[] AllowedOrigins { get; set; } = [];
        public bool AllowCredentials { get; set; } = true;
    }
}

