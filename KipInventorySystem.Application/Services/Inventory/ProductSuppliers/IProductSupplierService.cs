using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.ProductSuppliers;

public interface IProductSupplierService
{
    Task<ServiceResponse<ProductSupplierDTO>> CreateAsync(
        Guid productId,
        CreateProductSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ProductSupplierDTO>> UpdateAsync(
        Guid productId,
        Guid supplierId,
        UpdateProductSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> DeleteAsync(
        Guid productId,
        Guid supplierId,
        CancellationToken cancellationToken = default);
}
