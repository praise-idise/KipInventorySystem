using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Warehouses;
using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

/// <summary>
/// Manage warehouses and warehouse lookup endpoints.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class WarehousesController(IWarehouseService warehouseService) : BaseController
{
    /// <summary>
    /// List warehouses with pagination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputePagedResponse(await warehouseService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Search warehouses by key fields.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputePagedResponse(await warehouseService.SearchAsync(searchTerm, parameters, cancellationToken));

    /// <summary>
    /// Get a single warehouse by id.
    /// </summary>
    [HttpGet("{warehouseId:guid}")]
    public async Task<IActionResult> GetById(Guid warehouseId, CancellationToken cancellationToken)
        => ComputeResponse(await warehouseService.GetByIdAsync(warehouseId, cancellationToken));

    /// <summary>
    /// Create a new warehouse.
    /// </summary>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await warehouseService.CreateAsync(request, cancellationToken));
    }

    /// <summary>
    /// Update an existing warehouse.
    /// </summary>
    [HttpPatch("{warehouseId:guid}")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Update(
        Guid warehouseId,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await warehouseService.UpdateAsync(warehouseId, request, cancellationToken));
    }

    /// <summary>
    /// Soft delete a warehouse.
    /// </summary>
    [HttpDelete("{warehouseId:guid}")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> SoftDelete(Guid warehouseId, CancellationToken cancellationToken)
        => ComputeResponse(await warehouseService.SoftDeleteAsync(warehouseId, cancellationToken));
}
