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

    Task<ServiceResponse<List<ApprovalRequestDto>>> GetHistoryAsync(
        ApprovalDocumentType documentType,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
