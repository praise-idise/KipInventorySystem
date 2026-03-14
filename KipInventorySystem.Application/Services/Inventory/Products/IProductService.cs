using KipInventorySystem.Shared.Responses;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.Products;

public interface IProductService
{
    Task<ServiceResponse<ProductDTO>> CreateAsync(
        CreateProductDTO request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ProductDTO>> UpdateAsync(
        Guid productId,
        UpdateProductDTO request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> SoftDeleteAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<ProductDTO>> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<ProductDTO>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<ProductDTO>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
