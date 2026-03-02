using KipInventorySystem.Application.Services.Auth.DTOs;
using KipInventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Http;

namespace KipInventorySystem.Application.Services.Auth;

public interface IAuthService
{
    Task<ServiceResponse<LoginResponseDTO>> SignupAsync(RegisterDTO model);
    Task<ServiceResponse<LoginResponseDTO>> LoginAsync(LoginDTO model);
    Task<ServiceResponse<LoginResponseDTO>> RefreshTokenAsync();
    Task<ServiceResponse> LogoutAsync();
    Task<ServiceResponse> RevokeUserSessionsAsync(string userId);
    Task<ServiceResponse> ChangePasswordAsync(ChangePasswordDTO model);
    Task<ServiceResponse> ForgotPasswordAsync(ForgotPasswordDTO model);
    Task<ServiceResponse> ResetPasswordAsync(ResetPasswordDTO model);
}
