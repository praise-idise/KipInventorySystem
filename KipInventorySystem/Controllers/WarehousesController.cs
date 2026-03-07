using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Warehouses;
using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WarehousesController(IWarehouseService warehouseService) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await warehouseService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await warehouseService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{warehouseId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid warehouseId, CancellationToken cancellationToken)
        => ComputeResponse(await warehouseService.GetByIdAsync(warehouseId, cancellationToken));

    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await warehouseService.CreateAsync(request, cancellationToken));
    }

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

    [HttpDelete("{warehouseId:guid}")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> SoftDelete(Guid warehouseId, CancellationToken cancellationToken)
        => ComputeResponse(await warehouseService.SoftDeleteAsync(warehouseId, cancellationToken));
}
