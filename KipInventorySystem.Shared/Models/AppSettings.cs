using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Shared.Models;

public static class AppSettings
{
    public class JwtSettings
    {
        [Required]
        public string SecretKey { get; set; } = string.Empty;
        [Required]
        public string Issuer { get; set; } = string.Empty;
        [Required]
        public string Audience { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int ExpirationMinutes { get; set; }
        [Range(1, int.MaxValue)]
        public int RefreshTokenDays { get; set; }
    }

    public class FrontendSettings
    {

        [Required]
        public string BaseUrl { get; set; } = string.Empty;  // Base URL of the frontend, e.g. https://app.example.com
        [Required]
        public string ResetPasswordPath { get; set; } = string.Empty; // Path used for reset password flow (no leading slash), default is "reset-password"
        [Required]
        public string VerifyEmailPath { get; set; } = string.Empty; // Path used for email verification flow (no leading slash), default is "verify-email"
    }

    public class AdminSettings
    {
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string FirstName { get; set; } = string.Empty;
        [Required]
        public string LastName { get; set; } = string.Empty;
    }

    public class CloudinarySettings
    {
        [Required]
        public string CloudName { get; set; } = string.Empty;
        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string ApiSecret { get; set; } = string.Empty;
        [Required]
        public string FolderPrefix { get; set; } = string.Empty;
    }

    public class SmtpSettings
    {
        [Required]
        public string Host { get; set; } = string.Empty;
        [Required]
        public int Port { get; set; }
        [Required]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string FromAddress { get; set; } = string.Empty;
        [Required]
        public string FromName { get; set; } = string.Empty;
    }

    public class StripeSettings
    {
        [Required]
        public string SecretKey { get; set; } = string.Empty;
        [Required]
        public string PublishableKey { get; set; } = string.Empty;
        [Required]
        public string WebhookSecret { get; set; } = string.Empty;
    }

    public class HangfireSettings
    {
        [Required]
        public string DashboardUsername { get; set; } = string.Empty;
        [Required]
        public string DashboardPassword { get; set; } = string.Empty;
    }

    public class CorsSettings
    {
        [Required]
        public string[] AllowedOrigins { get; set; } = [];
        public bool AllowCredentials { get; set; } = true;
    }
}

