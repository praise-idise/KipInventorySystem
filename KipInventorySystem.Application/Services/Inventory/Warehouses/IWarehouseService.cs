using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.Warehouses;

public interface IWarehouseService
{
    Task<ServiceResponse<WarehouseDto>> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<WarehouseDto>> UpdateAsync(
        Guid warehouseId,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> SoftDeleteAsync(Guid warehouseId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<WarehouseDto>> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<WarehouseDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<WarehouseDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
