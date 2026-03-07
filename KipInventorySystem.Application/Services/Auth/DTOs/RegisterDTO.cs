using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class RegisterDTO
{
    [EmailAddress]
    [Required]
    [DefaultValue("admin@example.com")]
    public string Email { get; set; } = null!;

    [Required]
    [DefaultValue("AdminUser123$")]
    public string Password { get; set; } = null!;

    [Required]
    [DefaultValue("Admin")]
    public string FirstName { get; set; } = null!;

    [Required]
    [DefaultValue("User")]
    public string LastName { get; set; } = null!;

    [Phone]
    [DefaultValue("+2348012345678")]
    public string? PhoneNumber { get; set; }
}
