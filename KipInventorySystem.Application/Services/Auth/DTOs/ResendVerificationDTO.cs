using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class ResendVerificationDTO
{
    public string Email { get; set; } = string.Empty;
}
