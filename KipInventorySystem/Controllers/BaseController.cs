using Asp.Versioning;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected IActionResult ComputeResponse<T>(ServiceResponse<T> serviceResponse)
    {
        var result = new ApiResponse<T>(
            serviceResponse.Succeeded,
            (int)serviceResponse.StatusCode,
            serviceResponse.Message,
            serviceResponse.Data
        );

        return StatusCode(result.StatusCode, result);
    }

    protected IActionResult ComputeResponse(ServiceResponse serviceResponse)
    {
        var result = new
        {
            Success = serviceResponse.Succeeded,
            StatusCode = (int)serviceResponse.StatusCode,
            Message = serviceResponse.Message
        };

        return StatusCode((int)serviceResponse.StatusCode, result);
    }

    protected IActionResult ValidateModelState()
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));

            var response = ServiceResponse.BadRequest(errors);
            return ComputeResponse(response);
        }

        return null!;
    }

    protected bool TryGetIdempotencyKey(out string idempotencyKey, out IActionResult? errorResult)
    {
        idempotencyKey = string.Empty;
        errorResult = null;

        if (!Request.Headers.TryGetValue("X-Idempotency-Key", out var values))
        {
            errorResult = ComputeResponse(ServiceResponse.BadRequest("X-Idempotency-Key header is required."));
            return false;
        }

        idempotencyKey = values.ToString().Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            errorResult = ComputeResponse(ServiceResponse.BadRequest("X-Idempotency-Key header cannot be empty."));
            return false;
        }

        return true;
    }
}
