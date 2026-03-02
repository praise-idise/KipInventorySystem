using System;
using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class RegisterDTO
{
    [EmailAddress]
    [Required]
    public string Email { get; set; } = null!;
    [Required]
    public string Password { get; set; } = null!;
    [Required]
    public string FirstName { get; set; } = null!;
    [Required]
    public string LastName { get; set; } = null!;
    [Phone]
    public string? PhoneNumber { get; set; }
}
