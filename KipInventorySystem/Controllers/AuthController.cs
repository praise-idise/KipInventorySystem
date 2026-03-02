using Asp.Versioning;
using KipInventorySystem.Application.Services.Auth;
using KipInventorySystem.Application.Services.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

/// <summary>
/// Authentication Controller - handles signup, login, token refresh and password flows.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController(IAuthService authService) : BaseController
{
    private readonly IAuthService _authService = authService;

    /// <summary>
    /// Sign up a new user.
    /// </summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]

    public async Task<IActionResult> Signup([FromBody] RegisterDTO model)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        var result = await _authService.SignupAsync(model);
        return ComputeResponse(result);
    }

    /// <summary>
    /// Login an existing user.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginDTO model)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        var result = await _authService.LoginAsync(model);
        return ComputeResponse(result);
    }

    /// <summary>
    /// Refresh access token using refresh token cookie.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh()
    {
        var result = await _authService.RefreshTokenAsync();
        return ComputeResponse(result);
    }

    /// <summary>
    /// Logout current session (revokes refresh token cookie).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Logout()
    {
        var result = await _authService.LogoutAsync();
        return ComputeResponse(result);
    }

    /// <summary>
    /// Change password for authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO model)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        var result = await _authService.ChangePasswordAsync(model);
        return ComputeResponse(result);
    }

    /// <summary>
    /// Start forgot password flow (sends reset link if email exists).
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO model)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        var result = await _authService.ForgotPasswordAsync(model);
        return ComputeResponse(result);
    }

    /// <summary>
    /// Reset password using token sent to email.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO model)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        var result = await _authService.ResetPasswordAsync(model);
        return ComputeResponse(result);
    }

    /// <summary>
    /// Revoke all sessions for a user (administrative action).
    /// </summary>
    [HttpPost("revoke-sessions/{userId}")]
    [Roles(ROLE_TYPE.ADMIN)]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeUserSessions(string userId)
    {
        var result = await _authService.RevokeUserSessionsAsync(userId);
        return ComputeResponse(result);
    }
}
