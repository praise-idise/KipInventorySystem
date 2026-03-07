namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class LoginResponseDTO
{
    public string? Token { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public IEnumerable<string>? Roles { get; set; }
}
