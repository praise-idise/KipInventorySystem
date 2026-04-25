using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class RegisterDTO
{
    [DefaultValue("admin@example.com")]
    public string Email { get; set; } = null!;

    [DefaultValue("AdminUser123$")]
    public string Password { get; set; } = null!;

    [DefaultValue("Admin")]
    public string FirstName { get; set; } = null!;

    [DefaultValue("User")]
    public string LastName { get; set; } = null!;

    [DefaultValue("+2348012345678")]
    public string? PhoneNumber { get; set; }
}
