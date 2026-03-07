using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Auth.DTOs;

public class ChangePasswordDTO
{
    [DefaultValue("Currentpassword123$")]
    public string CurrentPassword { get; set; } = string.Empty;

    [DefaultValue("Newpassword123$")]
    public string NewPassword { get; set; } = string.Empty;
}
