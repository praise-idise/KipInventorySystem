using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.Infrastructure.Seeder;

public class ApplicationSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<AdminSettings> adminSettings,
    ILogger<ApplicationSeeder> logger) : IApplicationSeeder
{
    private readonly AdminSettings _adminSettings = adminSettings.Value;

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedAdminUserAsync();
    }

    private async Task SeedRolesAsync()
    {
        // Create roles based on ROLE_TYPE enum
        foreach (var roleName in Enum.GetNames<ROLE_TYPE>())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };

                var result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create role {Role}: {Errors}", roleName, errors);
                }
                else
                {
                    logger.LogInformation("Created role {Role}", roleName);
                }
            }
            else
            {
                logger.LogDebug("Role {Role} already exists", roleName);
            }
        }
    }

    private async Task SeedAdminUserAsync()
    {
        // Check if admin user already exists
        var adminUser = await userManager.FindByNameAsync(_adminSettings.Username);
        
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = _adminSettings.Username,
                Email = _adminSettings.Email,
                EmailConfirmed = true,
                FirstName = _adminSettings.FirstName,
                LastName = _adminSettings.LastName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, _adminSettings.Password);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to create admin user: {Errors}", errors);
                return;
            }

            logger.LogInformation("Created admin user: {Username}", _adminSettings.Username);

            // Assign ADMIN role to the user
            var roleResult = await userManager.AddToRoleAsync(adminUser, ROLE_TYPE.ADMIN.ToString());
            
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to assign ADMIN role to user {Username}: {Errors}", _adminSettings.Username, errors);
            }
            else
            {
                logger.LogInformation("Assigned ADMIN role to user: {Username}", _adminSettings.Username);
            }
        }
        else
        {
            logger.LogDebug("Admin user {Username} already exists", _adminSettings.Username);
        }
    }
}
