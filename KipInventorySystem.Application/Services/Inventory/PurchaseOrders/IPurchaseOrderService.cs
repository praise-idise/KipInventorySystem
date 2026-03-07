using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.PurchaseOrders;

public interface IPurchaseOrderService
{
    Task<ServiceResponse<PurchaseOrderDto>> CreateDraftAsync(
        CreatePurchaseOrderDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PurchaseOrderDto>> UpdateDraftAsync(
        Guid purchaseOrderId,
        UpdatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PurchaseOrderDto>> SubmitAsync(
        Guid purchaseOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PurchaseOrderDto>> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<PurchaseOrderDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<PurchaseOrderDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
