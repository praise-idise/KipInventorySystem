namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class ActiveSessionDTO
{
    public string SessionId { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

}
