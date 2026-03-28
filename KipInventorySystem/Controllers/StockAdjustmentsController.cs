using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class StockAdjustmentsController(IStockAdjustmentService stockAdjustmentService) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await stockAdjustmentService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await stockAdjustmentService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{stockAdjustmentId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid stockAdjustmentId, CancellationToken cancellationToken)
        => ComputeResponse(await stockAdjustmentService.GetByIdAsync(stockAdjustmentId, cancellationToken));

    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateStockAdjustmentDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockAdjustmentService.CreateDraftAsync(request, key, cancellationToken));
    }

    [HttpPost("{stockAdjustmentId:guid}/submit")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Submit(Guid stockAdjustmentId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockAdjustmentService.SubmitAsync(stockAdjustmentId, key, cancellationToken));
    }

    [HttpPost("{stockAdjustmentId:guid}/approve")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Approve(Guid stockAdjustmentId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockAdjustmentService.ApproveAsync(stockAdjustmentId, key, cancellationToken));
    }

    [HttpPost("{stockAdjustmentId:guid}/return")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> ReturnForChanges(
        Guid stockAdjustmentId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockAdjustmentService.ReturnForChangesAsync(stockAdjustmentId, request, key, cancellationToken));
    }

    [HttpPost("{stockAdjustmentId:guid}/apply")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Apply(Guid stockAdjustmentId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockAdjustmentService.ApplyAsync(stockAdjustmentId, key, cancellationToken));
    }

    [HttpPost("{stockAdjustmentId:guid}/cancel")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Cancel(Guid stockAdjustmentId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockAdjustmentService.CancelAsync(stockAdjustmentId, key, cancellationToken));
    }
}
