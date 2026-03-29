using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.SalesOrders;
using KipInventorySystem.Application.Services.Inventory.SalesOrders.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class SalesOrdersController(ISalesOrderService salesOrderService) : BaseController
{
    /// <summary>
    /// List sales orders with pagination.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await salesOrderService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Search sales orders.
    /// </summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await salesOrderService.SearchAsync(searchTerm, parameters, cancellationToken));

    /// <summary>
    /// Get a single sales order by id.
    /// </summary>
    [HttpGet("{salesOrderId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid salesOrderId, CancellationToken cancellationToken)
        => ComputeResponse(await salesOrderService.GetByIdAsync(salesOrderId, cancellationToken));

    /// <summary>
    /// Create a draft sales order.
    /// </summary>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateSalesOrderDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await salesOrderService.CreateDraftAsync(request, key, cancellationToken));
    }

    /// <summary>
    /// Update a draft sales order.
    /// </summary>
    [HttpPatch("{salesOrderId:guid}/draft")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    public async Task<IActionResult> UpdateDraft(
        Guid salesOrderId,
        [FromBody] UpdateSalesOrderDraftRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await salesOrderService.UpdateDraftAsync(salesOrderId, request, cancellationToken));
    }

    /// <summary>
    /// Confirm a sales order and reserve stock.
    /// </summary>
    [HttpPost("{salesOrderId:guid}/confirm")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Confirm(Guid salesOrderId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await salesOrderService.ConfirmAsync(salesOrderId, key, cancellationToken));
    }

    /// <summary>
    /// Fulfill reserved quantities on a sales order.
    /// </summary>
    [HttpPost("{salesOrderId:guid}/fulfill")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Fulfill(
        Guid salesOrderId,
        [FromBody] FulfillSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await salesOrderService.FulfillAsync(salesOrderId, request, key, cancellationToken));
    }

    /// <summary>
    /// Cancel a sales order and release any reservations.
    /// </summary>
    [HttpPost("{salesOrderId:guid}/cancel")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Cancel(Guid salesOrderId, CancellationToken cancellationToken)
    {
        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await salesOrderService.CancelAsync(salesOrderId, key, cancellationToken));
    }
}
