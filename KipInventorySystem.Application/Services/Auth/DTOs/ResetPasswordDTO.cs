using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class ResetPasswordDTO
{
    [DefaultValue("admin@example.com")]
    public string Email { get; set; } = string.Empty;

    [DefaultValue("CfDJ8KpR6fQ0...sample-token")]
    public string Token { get; set; } = string.Empty;

    [DefaultValue("AdminUser1234$")]
    public string NewPassword { get; set; } = string.Empty;
}
