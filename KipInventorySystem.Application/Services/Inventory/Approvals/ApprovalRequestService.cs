using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;

namespace KipInventorySystem.Application.Services.Inventory.Approvals;

public class ApprovalRequestService(
    IUnitOfWork unitOfWork,
    IMapper mapper) : IApprovalRequestService
{
    public async Task<ServiceResponse<PaginationResult<ApprovalRequestDto>>> GetPendingAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var approvals = await unitOfWork.Repository<ApprovalRequest>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            x => x.Status == ApprovalDecisionStatus.Pending,
            cancellationToken);

        var response = new PaginationResult<ApprovalRequestDto>
        {
            Records = [.. approvals.Records.Select(x => mapper.Map<ApprovalRequestDto>(x))],
            TotalRecords = approvals.TotalRecords,
            PageSize = approvals.PageSize,
            CurrentPage = approvals.CurrentPage
        };

        return ServiceResponse<PaginationResult<ApprovalRequestDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<ApprovalRequestDto>>> GetHistoryAsync(
        ApprovalDocumentType documentType,
        Guid documentId,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var approvals = await unitOfWork.Repository<ApprovalRequest>().GetPagedItemsAsync(
                parameters,
                query => query.OrderByDescending(x => x.RequestedAt),
            x => x.DocumentType == documentType && x.DocumentId == documentId,
             cancellationToken);

        var response = new PaginationResult<ApprovalRequestDto>
        {
            Records = [.. approvals.Records.Select(x => mapper.Map<ApprovalRequestDto>(x))],
            TotalRecords = approvals.TotalRecords,
            PageSize = approvals.PageSize,
            CurrentPage = approvals.CurrentPage
        };

        return ServiceResponse<PaginationResult<ApprovalRequestDto>>.Success(response);
    }
}
