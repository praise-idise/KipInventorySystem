using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.Approvals;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

/// <summary>
/// View pending approval work and approval history for inventory documents.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ApprovalsController(IApprovalRequestService approvalRequestService) : BaseController
{
    /// <summary>
    /// List pending approval requests.
    /// </summary>
    [HttpGet("pending")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    public async Task<IActionResult> GetPending([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await approvalRequestService.GetPendingAsync(parameters, cancellationToken));

    /// <summary>
    /// Get approval history for a document.
    /// </summary>
    [HttpGet("{documentType}/{documentId:guid}/history")]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.APPROVER)]
    public async Task<IActionResult> GetHistory(
        ApprovalDocumentType documentType,
        Guid documentId,
        CancellationToken cancellationToken)
        => ComputeResponse(await approvalRequestService.GetHistoryAsync(documentType, documentId, cancellationToken));
}
