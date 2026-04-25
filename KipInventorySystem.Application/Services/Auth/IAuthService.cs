using KipInventorySystem.Application.Services.Auth.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Auth;

public interface IAuthService
{
    Task<ServiceResponse> SignupAsync(RegisterDTO model);
    Task<ServiceResponse<LoginResponseDTO>> LoginAsync(LoginDTO model);
    Task<ServiceResponse<LoginResponseDTO>> RefreshTokenAsync();
    Task<ServiceResponse> LogoutAsync();
    Task<ServiceResponse> RevokeUserSessionsAsync(string userId);
    Task<ServiceResponse> ChangePasswordAsync(ChangePasswordDTO model);
    Task<ServiceResponse> ForgotPasswordAsync(ForgotPasswordDTO model);
    Task<ServiceResponse> ResetPasswordAsync(ResetPasswordDTO model);
    Task<ServiceResponse<LoginResponseDTO>> VerifyEmailAsync(string email, string token);
    Task<ServiceResponse> ResendVerificationEmailAsync(string email);
}
