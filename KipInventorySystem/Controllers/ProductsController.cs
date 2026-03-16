using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Products;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController(IProductService productService) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await productService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await productService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{productId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid productId, CancellationToken cancellationToken)
        => ComputeResponse(await productService.GetByIdAsync(productId, cancellationToken));

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

    [HttpDelete("{productId:guid}")]
    [Roles(ROLE_TYPE.ADMIN)]
    public async Task<IActionResult> SoftDelete(Guid productId, CancellationToken cancellationToken)
        => ComputeResponse(await productService.SoftDeleteAsync(productId, cancellationToken));
}
