using System;
using System.Collections.Generic;
using System.Text;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Shared.Interfaces;

public interface IUserContext
{

    /// <summary>
    /// Gets the current authenticated user from the HTTP context
    /// </summary>
    /// <returns>
    /// CurrentUser object containing user information and claims.
    /// Returns an anonymous user if not authenticated.
    /// </returns>
    CurrentUser GetCurrentUser();

    /// <summary>
    /// Checks if the current user is authenticated
    /// </summary>
    /// <returns>True if authenticated, false otherwise</returns>
    bool IsAuthenticated();

    /// <summary>
    /// Checks if the current user has a specific role
    /// </summary>
    /// <param name="role">The role name to check (case-insensitive)</param>
    /// <returns>True if current user has the role, false otherwise</returns>
    bool IsInRole(string role);

    /// <summary>
    /// Checks if the current user is an ADMIN
    /// </summary>
    /// <returns>True if current user is ADMIN, false otherwise</returns>
    bool IsAdmin();


    string? IpAddress { get; }
    string? UserAgent { get; }
    string? GetCookie(string name);
    void SetCookie(string name, string value, DateTime expires);
    void DeleteCookie(string name);
}
