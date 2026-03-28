using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.StockAdjustments;

public interface IStockAdjustmentService
{
    Task<ServiceResponse<StockAdjustmentDto>> CreateDraftAsync(
        CreateStockAdjustmentDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<StockAdjustmentDto>> SubmitAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<StockAdjustmentDto>> ApproveAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<StockAdjustmentDto>> ApplyAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<StockAdjustmentDto>> ReturnForChangesAsync(
        Guid stockAdjustmentId,
        ApprovalDecisionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<StockAdjustmentDto>> CancelAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<StockAdjustmentDto>> GetByIdAsync(Guid stockAdjustmentId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<StockAdjustmentDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<StockAdjustmentDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
