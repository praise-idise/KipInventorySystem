using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class ForgotPasswordDTO
{
    [DefaultValue("admin@example.com")]
    public string Email { get; set; } = string.Empty;
}
