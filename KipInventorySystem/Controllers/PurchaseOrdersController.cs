using Asp.Versioning;
using KipInventorySystem.API.Attributes;
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
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await purchaseOrderService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await purchaseOrderService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{purchaseOrderId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid purchaseOrderId, CancellationToken cancellationToken)
        => ComputeResponse(await purchaseOrderService.GetByIdAsync(purchaseOrderId, cancellationToken));

    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN)]
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

    [HttpPatch("{purchaseOrderId:guid}/draft")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> UpdateDraft(
        Guid purchaseOrderId,
        [FromBody] UpdatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await purchaseOrderService.UpdateDraftAsync(purchaseOrderId, request, cancellationToken));
    }

    [HttpPost("{purchaseOrderId:guid}/submit")]
    [Roles(ROLE_TYPE.ADMIN)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Submit(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await purchaseOrderService.SubmitAsync(purchaseOrderId, key, cancellationToken));
    }
}
