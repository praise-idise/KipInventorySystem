using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace KipInventorySystem.API.Middlewares;

/// <summary>
/// Custom authorization middleware result handler that returns JSON responses for authorization failures
/// </summary>
public class CustomAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        // If authorization failed, write JSON response instead of default empty 401/403
        if (!authorizeResult.Succeeded && !context.Response.HasStarted)
        {
            // Check if user is actually authenticated
            // If not authenticated, return 401 (authentication issue)
            // If authenticated, return 403 (authorization/permission issue)
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                // User is not authenticated - return 401
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    Success = false,
                    StatusCode = 401,
                    Message = "Authentication required. Please provide a valid access token."
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response, _jsonOptions)
                );
                return;
            }

            // User is authenticated but lacks permission - return 403
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var forbiddenResponse = new
            {
                Success = false,
                StatusCode = 403,
                Message = "You do not have permission to access this resource."
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(forbiddenResponse, _jsonOptions)
            );
            return;
        }

        // For successful authorization or if response already started, use default handler
        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
