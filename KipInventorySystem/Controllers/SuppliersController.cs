using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Suppliers;
using KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class SuppliersController(IInventorySupplierService supplierService) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await supplierService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await supplierService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{supplierId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid supplierId, CancellationToken cancellationToken)
        => ComputeResponse(await supplierService.GetByIdAsync(supplierId, cancellationToken));

    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await supplierService.CreateAsync(request, cancellationToken));
    }

    [HttpPatch("{supplierId:guid}")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> Update(
        Guid supplierId,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await supplierService.UpdateAsync(supplierId, request, cancellationToken));
    }

    [HttpDelete("{supplierId:guid}")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> SoftDelete(Guid supplierId, CancellationToken cancellationToken)
        => ComputeResponse(await supplierService.SoftDeleteAsync(supplierId, cancellationToken));
}
