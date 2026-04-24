using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.Approvals;

public interface IApprovalRequestService
{
    Task<ServiceResponse<PaginationResult<ApprovalRequestDto>>> GetPendingAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<ApprovalRequestDto>>> GetHistoryAsync(
        ApprovalDocumentType documentType,
        Guid documentId,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
