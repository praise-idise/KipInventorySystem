using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Products;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

/// <summary>
/// Manage products and the supplier links attached to them.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController(
    IProductService productService,
    IProductSupplierService productSupplierService) : BaseController
{
    /// <summary>
    /// List products with pagination.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await productService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Search products by key fields.
    /// </summary>
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await productService.SearchAsync(searchTerm, parameters, cancellationToken));

    /// <summary>
    /// Get a single product by id.
    /// </summary>
    [HttpGet("{productId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid productId, CancellationToken cancellationToken)
        => ComputeResponse(await productService.GetByIdAsync(productId, cancellationToken));

    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Create([FromBody] CreateProductDTO request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        var response = await productService.CreateAsync(request, key, cancellationToken);
        return ComputeResponse(response);
    }

    /// <summary>
    /// Update an existing product.
    /// </summary>
    [HttpPatch("{productId:guid}")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> Update(
        Guid productId,
        [FromBody] UpdateProductDTO request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        var response = await productService.UpdateAsync(productId, request, cancellationToken);
        return ComputeResponse(response);
    }

    /// <summary>
    /// Link a supplier to a product.
    /// </summary>
    [HttpPost("{productId:guid}/suppliers")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> CreateSupplierLink(
        Guid productId,
        [FromBody] CreateProductSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await productSupplierService.CreateAsync(productId, request, cancellationToken));
    }

    /// <summary>
    /// Update a product-supplier link.
    /// </summary>
    [HttpPatch("{productId:guid}/suppliers/{supplierId:guid}")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> UpdateSupplierLink(
        Guid productId,
        Guid supplierId,
        [FromBody] UpdateProductSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        return ComputeResponse(await productSupplierService.UpdateAsync(productId, supplierId, request, cancellationToken));
    }

    /// <summary>
    /// Remove a supplier link from a product.
    /// </summary>
    [HttpDelete("{productId:guid}/suppliers/{supplierId:guid}")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.PROCUREMENT_OFFICER)]
    public async Task<IActionResult> DeleteSupplierLink(
        Guid productId,
        Guid supplierId,
        CancellationToken cancellationToken)
        => ComputeResponse(await productSupplierService.DeleteAsync(productId, supplierId, cancellationToken));

    /// <summary>
    /// Soft delete a product.
    /// </summary>
    [HttpDelete("{productId:guid}")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> SoftDelete(Guid productId, CancellationToken cancellationToken)
        => ComputeResponse(await productService.SoftDeleteAsync(productId, cancellationToken));
}
