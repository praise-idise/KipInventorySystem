using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PurchaseOrdersController(IPurchaseOrderService purchaseOrderService) : BaseController
{
    /// <summary>
    /// List purchase orders with pagination.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await purchaseOrderService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Search purchase orders.
    /// </summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await purchaseOrderService.SearchAsync(searchTerm, parameters, cancellationToken));

    /// <summary>
    /// Get a single purchase order by id.
    /// </summary>
    [HttpGet("{purchaseOrderId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid purchaseOrderId, CancellationToken cancellationToken)
        => ComputeResponse(await purchaseOrderService.GetByIdAsync(purchaseOrderId, cancellationToken));

    /// <summary>
    /// Create a draft purchase order.
    /// </summary>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await purchaseOrderService.CreateDraftAsync(request, key, cancellationToken));
    }

    /// <summary>
    /// Update a draft purchase order.
    /// </summary>
    [HttpPatch("{purchaseOrderId:guid}/draft")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> UpdateDraft(
        Guid purchaseOrderId,
        [FromBody] UpdatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await purchaseOrderService.UpdateDraftAsync(purchaseOrderId, request, cancellationToken));
    }

    /// <summary>
    /// Submit a purchase order for approval.
    /// </summary>
    [HttpPost("{purchaseOrderId:guid}/submit")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Submit(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await purchaseOrderService.SubmitAsync(purchaseOrderId, key, cancellationToken));
    }

    /// <summary>
    /// Approve a purchase order.
    /// </summary>
    [HttpPost("{purchaseOrderId:guid}/approve")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Approve(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await purchaseOrderService.ApproveAsync(purchaseOrderId, key, cancellationToken));
    }

    /// <summary>
    /// Return a purchase order for changes.
    /// </summary>
    [HttpPost("{purchaseOrderId:guid}/return")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> ReturnForChanges(
        Guid purchaseOrderId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await purchaseOrderService.ReturnForChangesAsync(purchaseOrderId, request, key, cancellationToken));
    }
}
