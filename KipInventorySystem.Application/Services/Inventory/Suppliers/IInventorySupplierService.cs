using KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.Suppliers;

public interface IInventorySupplierService
{
    Task<ServiceResponse<SupplierDto>> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<SupplierDto>> UpdateAsync(
        Guid supplierId,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> SoftDeleteAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<SupplierDto>> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<SupplierDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<SupplierDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
