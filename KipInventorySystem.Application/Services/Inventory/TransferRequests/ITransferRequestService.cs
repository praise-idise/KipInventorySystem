using KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.TransferRequests;

public interface ITransferRequestService
{
    Task<ServiceResponse<TransferRequestDto>> CreateDraftAsync(
        CreateTransferRequestDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<TransferRequestDto>> SubmitAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<TransferRequestDto>> DispatchAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<TransferRequestDto>> CompleteAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<TransferRequestDto>> CancelAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<TransferRequestDto>> GetByIdAsync(Guid transferRequestId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<TransferRequestDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<TransferRequestDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
