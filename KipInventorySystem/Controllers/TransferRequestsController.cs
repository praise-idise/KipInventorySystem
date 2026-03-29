using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Application.Services.Inventory.TransferRequests;
using KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TransferRequestsController(ITransferRequestService transferRequestService) : BaseController
{
    /// <summary>
    /// List transfer requests with pagination.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await transferRequestService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Search transfer requests.
    /// </summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await transferRequestService.SearchAsync(searchTerm, parameters, cancellationToken));

    /// <summary>
    /// Get a single transfer request by id.
    /// </summary>
    [HttpGet("{transferRequestId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid transferRequestId, CancellationToken cancellationToken)
        => ComputeResponse(await transferRequestService.GetByIdAsync(transferRequestId, cancellationToken));

    /// <summary>
    /// Create a draft transfer request.
    /// </summary>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateTransferRequestDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.CreateDraftAsync(request, key, cancellationToken));
    }

    /// <summary>
    /// Submit a transfer request for approval.
    /// </summary>
    [HttpPost("{transferRequestId:guid}/submit")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Submit(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.SubmitAsync(transferRequestId, key, cancellationToken));
    }

    /// <summary>
    /// Approve a transfer request.
    /// </summary>
    [HttpPost("{transferRequestId:guid}/approve")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Approve(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.ApproveAsync(transferRequestId, key, cancellationToken));
    }

    /// <summary>
    /// Return a transfer request for changes.
    /// </summary>
    [HttpPost("{transferRequestId:guid}/return")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> ReturnForChanges(
        Guid transferRequestId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.ReturnForChangesAsync(transferRequestId, request, key, cancellationToken));
    }

    /// <summary>
    /// Dispatch stock for an approved transfer request.
    /// </summary>
    [HttpPost("{transferRequestId:guid}/dispatch")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Dispatch(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.DispatchAsync(transferRequestId, key, cancellationToken));
    }

    /// <summary>
    /// Complete an in-transit transfer request.
    /// </summary>
    [HttpPost("{transferRequestId:guid}/complete")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Complete(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.CompleteAsync(transferRequestId, key, cancellationToken));
    }

    /// <summary>
    /// Cancel a transfer request.
    /// </summary>
    [HttpPost("{transferRequestId:guid}/cancel")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Cancel(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.CancelAsync(transferRequestId, key, cancellationToken));
    }
}
