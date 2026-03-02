namespace KipInventorySystem.Shared.Models;

public class CurrentUser(
    string userId,
    string email,
    IEnumerable<string> roles,
    string? firstName = null,
    string? lastName = null)
{
    public string UserId { get; init; } = userId;
    public string Email { get; init; } = email;
    public string? FirstName { get; init; } = firstName;
    public string? LastName { get; init; } = lastName;
    public IEnumerable<string> Roles { get; init; } = roles ?? [];

    /// <summary>
    /// Gets the full name of the user
    /// </summary>
    public string FullName => string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName)
        ? Email
        : $"{FirstName} {LastName}";

    /// <summary>
    /// Checks if the user has a specific role (case-insensitive)
    /// </summary>
    /// <param name="role">The role to check</param>
    /// <returns>True if the user has the role, false otherwise</returns>
    public bool IsInRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the user has any of the specified roles (case-insensitive)
    /// </summary>
    /// <param name="roles">The roles to check</param>
    /// <returns>True if the user has any of the roles, false otherwise</returns>
    public bool IsInAnyRole(params string[] roles)
    {
        return roles.Any(role => IsInRole(role));
    }

    /// <summary>
    /// Checks if this is an anonymous/unauthenticated user
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

    /// <summary>
    /// Creates an anonymous (unauthenticated) user
    /// </summary>
    public static CurrentUser Anonymous() => new(
        userId: string.Empty,
        email: string.Empty,
        roles: []
    );
}
