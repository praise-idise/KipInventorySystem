using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class LoginDTO
{
    [DefaultValue("admin@example.com")]
    public required string Email { get; set; }

    [DefaultValue("AdminUser123$")]
    public required string Password { get; set; }
}
