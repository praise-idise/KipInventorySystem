using Asp.Versioning;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ProducesResponseType(typeof(ApiResponse<object>), 400)]
[ProducesResponseType(typeof(ApiResponse<object>), 403)]
[ProducesResponseType(typeof(ApiResponse<object>), 404)]
[ProducesResponseType(typeof(ApiResponse<object>), 409)]
[ProducesResponseType(typeof(ApiResponse<object>), 500)]
[ProducesResponseType(typeof(ApiResponse<object>), 503)]
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

        ApiResponse<object> result = new(
        serviceResponse.Succeeded,
        (int)serviceResponse.StatusCode,
        serviceResponse.Message,
        null
    );

        return StatusCode((int)serviceResponse.StatusCode, result);
    }

    protected IActionResult ComputePagedResponse<TItem>(ServiceResponse<PaginationResult<TItem>> serviceResponse)
    {
        PaginationMeta? pagination = null;

        if (serviceResponse.Succeeded && serviceResponse.Data is not null)
        {
            var d = serviceResponse.Data;
            pagination = new PaginationMeta(d.CurrentPage, d.PageSize, d.TotalRecords, d.TotalPages);
        }

        var result = new ApiResponse<IReadOnlyList<TItem>>(
            serviceResponse.Succeeded,
            (int)serviceResponse.StatusCode,
            serviceResponse.Message,
            serviceResponse.Data?.Records,
            pagination
        );

        return StatusCode(result.StatusCode, result);
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
