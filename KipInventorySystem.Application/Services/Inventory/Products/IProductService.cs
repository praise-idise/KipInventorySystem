using KipInventorySystem.Shared.Responses;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.Products;

public interface IProductService
{
    Task<ServiceResponse<ProductDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ProductDto>> UpdateAsync(
        Guid productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> SoftDeleteAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<ProductDto>> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<ProductDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<ProductDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
