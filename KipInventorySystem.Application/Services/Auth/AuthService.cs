using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KipInventorySystem.Application.Services.Auth.DTOs;
using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Services.Redis;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Responses;
using Hangfire;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.Application.Services.Auth;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IOptions<JwtSettings> jwtOptions,
    IOptions<FrontendSettings> frontendOptions,
    ILogger<AuthService> logger,
    IRedisService redis,
    IUserContext userContext,
    IMapper mapper
) : IAuthService
{

    public async Task<ServiceResponse<LoginResponseDTO>> SignupAsync(RegisterDTO model)
    {
        var existingUser = await userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
            return ServiceResponse<LoginResponseDTO>.Conflict("Email already exists");

        var user = mapper.Map<ApplicationUser>(model);

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return ServiceResponse<LoginResponseDTO>.BadRequest(
                string.Join(", ", result.Errors.Select(e => e.Description)));

        // default role
        await userManager.AddToRoleAsync(user, ROLE_TYPE.USER.ToString());

        // ---------- ENQUEUE WELCOME EMAIL ----------
        BackgroundJob.Enqueue<IEmailBackgroundJobs>(
            "emails",
            jobs => jobs.SendWelcomeEmailAsync(
                user.Email!,
                user.FirstName ?? "User",
                user.LastName ?? "",
                default));
        
        logger.LogInformation("Welcome email job enqueued for {Email}", user.Email);

        // ---------- SESSION CREATION ----------
        var sessionId = await CreateSessionAsync(user);
        logger.LogInformation(
            "User {UserId} signed up. Session {SessionId}",
            user.Id, sessionId);

        // ---------- ACCESS TOKEN ----------
        var accessToken = await GenerateJwtToken(user);

        return ServiceResponse<LoginResponseDTO>.Success(new LoginResponseDTO
        {
            Token = accessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpirationMinutes),
            Email = user.Email!,
            UserId = user.Id,
            Roles = await userManager.GetRolesAsync(user)
        }, "Signup successful");
    }



    public async Task<ServiceResponse<LoginResponseDTO>> LoginAsync(LoginDTO model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);

        if (user == null || !await userManager.CheckPasswordAsync(user, model.Password))
        {
            logger.LogWarning("Failed login attempt for {Email}", model.Email);
            return ServiceResponse<LoginResponseDTO>.Unauthorized("Invalid credentials");
        }

        var accessToken = await GenerateJwtToken(user);

        string sessionId;
        try
        {
            sessionId = await CreateSessionAsync(user);
        }
        catch (RedisUnavailableException ex)
        {
            logger.LogError(ex, "Redis unavailable while creating login session for user {UserId}", user.Id);
            return ServiceResponse<LoginResponseDTO>.Unavailable(
                "Service is temporarily unavailable. Please try again shortly.");
        }

        logger.LogInformation(
            "User {UserId} logged in. Session {SessionId}",
            user.Id, sessionId);

        return ServiceResponse<LoginResponseDTO>.Success(new LoginResponseDTO
        {
            Token = accessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpirationMinutes),
            Email = user.Email!,
            UserId = user.Id,
            Roles = await userManager.GetRolesAsync(user)
        });
    }

    public async Task<ServiceResponse<LoginResponseDTO>> RefreshTokenAsync()
    {
        var refreshToken = userContext.GetCookie("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
            return ServiceResponse<LoginResponseDTO>.Unauthorized("Missing refresh token");

        try
        {
            var refreshKey = $"auth:refresh:{refreshToken}";
            var sessionId = await redis.GetAsync(refreshKey);

            if (sessionId == null)
                return ServiceResponse<LoginResponseDTO>.Unauthorized("Invalid refresh token");

            var sessionJson = await redis.GetAsync($"auth:session:{sessionId}");
            if (sessionJson == null)
                return ServiceResponse<LoginResponseDTO>.Unauthorized("Session expired");

            var session = JsonSerializer.Deserialize<UserSession>(sessionJson)!;

            var user = await userManager.FindByIdAsync(session.UserId);
            if (user == null)
                return ServiceResponse<LoginResponseDTO>.Unauthorized("User not found");

            if (session.TokenVersion != user.TokenVersion)
            {
                await redis.RemoveAsync(refreshKey);
                return ServiceResponse<LoginResponseDTO>.Unauthorized("Session revoked");
            }


            // REFRESH TOKEN ROTATION
            await redis.RemoveAsync(refreshKey);

            var newRefreshToken = GenerateRefreshToken();
            var newRefreshKey = $"auth:refresh:{newRefreshToken}";
            var expiry = session.ExpiresAt - DateTime.UtcNow;

            await redis.SetAsync(newRefreshKey, sessionId, expiry);
            userContext.SetCookie("refresh_token", newRefreshToken, session.ExpiresAt);

            var newAccessToken = await GenerateJwtToken(user);

            return ServiceResponse<LoginResponseDTO>.Success(new LoginResponseDTO
            {
                Token = newAccessToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpirationMinutes),
                Email = user.Email!,
                UserId = user.Id,
                Roles = await userManager.GetRolesAsync(user)
            });
        }
        catch (RedisUnavailableException ex)
        {
            logger.LogError(ex, "Redis unavailable while refreshing auth session");
            return ServiceResponse<LoginResponseDTO>.Unavailable(
                "Service is temporarily unavailable. Please try again shortly.");
        }
    }


    public async Task<ServiceResponse> LogoutAsync()
    {
        var refreshToken = userContext.GetCookie("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            logger.LogInformation("Logout attempted without refresh token cookie — nothing to revoke");
            userContext.DeleteCookie("refresh_token");
            return ServiceResponse.Success("Logged out successfully");
        }

        try
        {
            var sessionId = await redis.GetAsync($"auth:refresh:{refreshToken}");
            if (sessionId == null) return ServiceResponse.NotFound("Session not found");

            var sessionJson = await redis.GetAsync($"auth:session:{sessionId}");
            if (sessionJson != null)
            {
                var session = JsonSerializer.Deserialize<UserSession>(sessionJson)!;
                await redis.RemoveFromSetAsync(
                    $"auth:user-sessions:{session.UserId}",
                    sessionId);
            }

            await redis.RemoveAsync($"auth:session:{sessionId}");
            await redis.RemoveAsync($"auth:refresh:{refreshToken}");

            userContext.DeleteCookie("refresh_token");

            logger.LogInformation("Session {SessionId} logged out", sessionId);
            return ServiceResponse.Success("Logged out successfully");
        }
        catch (RedisUnavailableException ex)
        {
            logger.LogError(ex, "Redis unavailable while logging out current session");
            return ServiceResponse.Unavailable(
                "Service is temporarily unavailable. Please try again shortly.");
        }
    }

    public async Task<ServiceResponse> RevokeUserSessionsAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return ServiceResponse.NotFound("User not found");

        user.TokenVersion++;
        await userManager.UpdateAsync(user);

        try
        {
            var sessionIds = await redis.GetSetMembersAsync($"auth:user-sessions:{userId}");

            foreach (var sessionId in sessionIds)
            {
                await redis.RemoveAsync($"auth:session:{sessionId}");
            }

            await redis.RemoveAsync($"auth:user-sessions:{userId}");

            logger.LogWarning("All sessions revoked for user {UserId}", userId);
            return ServiceResponse.Success("All sessions revoked");
        }
        catch (RedisUnavailableException ex)
        {
            logger.LogError(ex, "Redis unavailable while revoking sessions for user {UserId}", userId);
            return ServiceResponse.Unavailable(
                "Service is temporarily unavailable. Please try again shortly.");
        }
    }

    public async Task<ServiceResponse> ChangePasswordAsync(ChangePasswordDTO model)
    {
        var currentUser = userContext.GetCurrentUser();
        if (!currentUser.IsAuthenticated)
            return ServiceResponse.Unauthorized("Not authenticated");

        var user = await userManager.FindByIdAsync(currentUser.UserId);
        if (user == null)
            return ServiceResponse.NotFound("User not found");

        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
            return ServiceResponse.BadRequest(
                string.Join(", ", result.Errors.Select(e => e.Description)));

        // Invalidate all sessions
        var revokeSessionsResponse = await RevokeUserSessionsAsync(user.Id);
        if (!revokeSessionsResponse.Succeeded)
            return revokeSessionsResponse;

        logger.LogInformation("Password changed for user {UserId}", user.Id);

        // ---------- ENQUEUE PASSWORD CHANGED EMAIL ----------
        BackgroundJob.Enqueue<IEmailBackgroundJobs>(
            "emails",
            jobs => jobs.SendPasswordChangedEmailAsync(
                user.Email!,
                user.FirstName ?? "User",
                userContext.IpAddress ?? "unknown",
                userContext.UserAgent ?? "unknown",
                default));
        
        logger.LogInformation("Password changed notification email job enqueued for {Email}", user.Email);

        return ServiceResponse.Success("Password changed successfully");
    }


    public async Task<ServiceResponse> ForgotPasswordAsync(ForgotPasswordDTO model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);

        if (user == null || !await userManager.IsEmailConfirmedAsync(user))
        {
            // Do NOT leak existence
            return ServiceResponse.Success(
                "If the email exists, a reset link has been sent");
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        var frontend = frontendOptions.Value;
        if (string.IsNullOrWhiteSpace(frontend.BaseUrl))
            throw new InvalidOperationException("Frontend:BaseUrl is not configured.");
        if (string.IsNullOrWhiteSpace(frontend.ResetPasswordPath))
            throw new InvalidOperationException("Frontend:ResetPasswordPath is not configured.");

        var baseUrl = frontend.BaseUrl.TrimEnd('/');
        var path = frontend.ResetPasswordPath.TrimStart('/');

        var resetLink =
            $"{baseUrl}/{path}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

        // ---------- ENQUEUE PASSWORD RESET EMAIL ----------
        BackgroundJob.Enqueue<IEmailBackgroundJobs>(
            "emails",
            jobs => jobs.SendPasswordResetEmailAsync(
                user.Email!,
                user.FirstName ?? "User",
                resetLink,
                24,
                default));
        
        logger.LogInformation("Password reset email job enqueued for {Email}", user.Email);

        return ServiceResponse.Success("If the email exists, a reset link has been sent");
    }


    public async Task<ServiceResponse> ResetPasswordAsync(ResetPasswordDTO model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return ServiceResponse.BadRequest("Invalid request");

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (!result.Succeeded)
            return ServiceResponse.BadRequest(
                string.Join(", ", result.Errors.Select(e => e.Description)));

        // Invalidate all sessions
        var revokeSessionsResponse = await RevokeUserSessionsAsync(user.Id);
        if (!revokeSessionsResponse.Succeeded)
            return revokeSessionsResponse;

        logger.LogWarning("Password reset for user {UserId}", user.Id);

        // ---------- ENQUEUE PASSWORD RESET SUCCESS EMAIL ----------
        BackgroundJob.Enqueue<IEmailBackgroundJobs>(
            "emails",
            jobs => jobs.SendPasswordResetSuccessEmailAsync(
                user.Email!,
                user.FirstName ?? "User",
                userContext.IpAddress ?? "unknown",
                userContext.UserAgent ?? "unknown",
                default));
        
        logger.LogInformation("Password reset success email job enqueued for {Email}", user.Email);

        return ServiceResponse.Success("Password reset successful");
    }




    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        var jwtSettings = jwtOptions.Value;
        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new("token_version", user.TokenVersion.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey));

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtSettings.ExpirationMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private async Task<string> CreateSessionAsync(ApplicationUser user)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var refreshToken = GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);

        var session = new UserSession
        {
            SessionId = sessionId,
            UserId = user.Id,
            TokenVersion = user.TokenVersion,
            IpAddress = userContext.IpAddress ?? "unknown",
            UserAgent = userContext.UserAgent ?? "unknown",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpiry
        };

        var ttl = refreshExpiry - DateTime.UtcNow;

        await redis.SetAsync(
            $"auth:session:{sessionId}",
            JsonSerializer.Serialize(session),
            ttl);

        await redis.AddToSetAsync(
            $"auth:user-sessions:{user.Id}",
            sessionId);

        await redis.ExpireAsync(
            $"auth:user-sessions:{user.Id}",
            TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays));

        await redis.SetAsync(
            $"auth:refresh:{refreshToken}",
            sessionId,
            ttl);

        userContext.SetCookie("refresh_token", refreshToken, refreshExpiry);

        return sessionId;
    }

}
