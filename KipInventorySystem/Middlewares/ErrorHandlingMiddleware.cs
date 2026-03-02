using KipInventorySystem.Shared.Models;
using System.Net;
using System.Text.Json;

namespace KipInventorySystem.API.Middlewares;

public class ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger) : IMiddleware
{
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next.Invoke(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            await WriteJsonErrorResponse(context, "Something went wrong", HttpStatusCode.InternalServerError);
        }
    }

    private static async Task WriteJsonErrorResponse(HttpContext context, string message, HttpStatusCode statusCode)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiResponse<object>(
            Success: false,
            StatusCode: (int)statusCode,
            Message: message,
            Data: null
        );

        var json = JsonSerializer.Serialize(response, CachedJsonSerializerOptions);
        await context.Response.WriteAsync(json);
    }
}
