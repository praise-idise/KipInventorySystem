using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Customers;
using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class CustomersController(ICustomerService customerService) : BaseController
{
    /// <summary>
    /// List customers with pagination.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await customerService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Search customers by name, email, or phone.
    /// </summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await customerService.SearchAsync(searchTerm, parameters, cancellationToken));

    /// <summary>
    /// Get a single customer by id.
    /// </summary>
    [HttpGet("{customerId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid customerId, CancellationToken cancellationToken)
        => ComputeResponse(await customerService.GetByIdAsync(customerId, cancellationToken));

    /// <summary>
    /// Create a new customer record.
    /// </summary>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await customerService.CreateAsync(request, key, cancellationToken));
    }

    /// <summary>
    /// Update an existing customer.
    /// </summary>
    [HttpPatch("{customerId:guid}")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    public async Task<IActionResult> Update(
        Guid customerId,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await customerService.UpdateAsync(customerId, request, cancellationToken));
    }

    /// <summary>
    /// Soft delete a customer.
    /// </summary>
    [HttpDelete("{customerId:guid}")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    public async Task<IActionResult> SoftDelete(Guid customerId, CancellationToken cancellationToken)
        => ComputeResponse(await customerService.SoftDeleteAsync(customerId, cancellationToken));
}
