using Asp.Versioning;
using KipInventorySystem.API.Attributes;
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
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await transferRequestService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await transferRequestService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{transferRequestId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid transferRequestId, CancellationToken cancellationToken)
        => ComputeResponse(await transferRequestService.GetByIdAsync(transferRequestId, cancellationToken));

    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN)]
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

    [HttpPost("{transferRequestId:guid}/submit")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Submit(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.SubmitAsync(transferRequestId, key, cancellationToken));
    }

    [HttpPost("{transferRequestId:guid}/dispatch")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Dispatch(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.DispatchAsync(transferRequestId, key, cancellationToken));
    }

    [HttpPost("{transferRequestId:guid}/complete")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Complete(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.CompleteAsync(transferRequestId, key, cancellationToken));
    }

    [HttpPost("{transferRequestId:guid}/cancel")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Cancel(Guid transferRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await transferRequestService.CancelAsync(transferRequestId, key, cancellationToken));
    }
}
