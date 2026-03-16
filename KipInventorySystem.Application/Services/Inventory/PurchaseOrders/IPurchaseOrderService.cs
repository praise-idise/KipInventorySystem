using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.PurchaseOrders;

public interface IPurchaseOrderService
{
    Task<ServiceResponse<PurchaseOrderDTO>> CreateDraftAsync(
        CreatePurchaseOrderDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PurchaseOrderDTO>> UpdateDraftAsync(
        Guid purchaseOrderId,
        UpdatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PurchaseOrderDTO>> SubmitAsync(
        Guid purchaseOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PurchaseOrderDTO>> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<PurchaseOrderDTO>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<PurchaseOrderDTO>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
