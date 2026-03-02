using System.Security.Claims;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Http;

namespace KipInventorySystem.Shared.Services;

public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// Gets the current authenticated user from the HTTP context
    /// </summary>
    /// <returns>
    /// CurrentUser object containing user information and claims.
    /// Returns an anonymous user if not authenticated.
    /// </returns>
    public CurrentUser GetCurrentUser()
    {
        var claimsPrincipal = _httpContextAccessor.HttpContext?.User;

        // If no context or unauthenticated, return a default "anonymous" user
        if (claimsPrincipal?.Identity == null || !claimsPrincipal.Identity.IsAuthenticated)
        {
            return CurrentUser.Anonymous();
        }

        // JWT Bearer middleware automatically maps "sub" -> ClaimTypes.NameIdentifier and "email" -> ClaimTypes.Email
        var userId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var email = claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        // Extract additional custom claims from  AppUser (using camelCase as stored in JWT)
        var firstName = claimsPrincipal.FindFirst("firstName")?.Value;
        var lastName = claimsPrincipal.FindFirst("lastName")?.Value;


        // Extract all roles
        var roles = claimsPrincipal
            .Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();

        return new CurrentUser(
            userId: userId,
            email: email,
            roles: roles,
            firstName: firstName,
            lastName: lastName
        );
    }

    /// <summary>
    /// Checks if the current user is authenticated
    /// </summary>
    /// <returns>True if authenticated, false otherwise</returns>
    public bool IsAuthenticated()
    {
        var claimsPrincipal = _httpContextAccessor.HttpContext?.User;
        return claimsPrincipal?.Identity?.IsAuthenticated ?? false;
    }

    /// <summary>
    /// Checks if the current user has a specific role
    /// </summary>
    /// <param name="role">The role name to check (case-insensitive)</param>
    /// <returns>True if current user has the role, false otherwise</returns>
    public bool IsInRole(string role)
    {
        var currentUser = GetCurrentUser();
        return currentUser.IsInRole(role);
    }

    /// <summary>
    /// Checks if the current user is an ADMIN
    /// </summary>
    /// <returns>True if current user is ADMIN, false otherwise</returns>
    public bool IsAdmin()
    {
        var currentUser = GetCurrentUser();
        // Check for "ADMIN" role (case-insensitive via CurrentUser.IsInRole)
        return currentUser.IsInRole(ROLE_TYPE.ADMIN.ToString());
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public string? GetCookie(string name)
    {
        if (_httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(name, out var value) ?? false)
            return value;
        return null;
    }

    public void SetCookie(string name, string value, DateTime expires)
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Append(
            name,
            value,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });
    }

    public void DeleteCookie(string name)
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Delete(name);
    }
}
