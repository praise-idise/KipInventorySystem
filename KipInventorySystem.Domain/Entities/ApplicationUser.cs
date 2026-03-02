using Microsoft.AspNetCore.Identity;

namespace KipInventorySystem.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int TokenVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DeletedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public class UserSession
{
    public string SessionId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public int TokenVersion { get; set; }
    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}