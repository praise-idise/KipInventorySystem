using KipInventorySystem.Shared.Enums;
using Microsoft.AspNetCore.Authorization;

namespace KipInventorySystem.API.Attributes;

public class RolesAttribute : AuthorizeAttribute
{
    public RolesAttribute(params ROLE_TYPE[] roles)
    {
        Roles = string.Join(",", roles.Select(r => r.ToString()));
    }
}