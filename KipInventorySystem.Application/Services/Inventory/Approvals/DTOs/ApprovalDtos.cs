using System.ComponentModel;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;

public class ApprovalDecisionRequest
{
    [DefaultValue("Please update the quantities to match the latest count sheet.")]
    public string Comment { get; set; } = string.Empty;
}

public class ApprovalRequestDto
{
    public Guid ApprovalRequestId { get; set; }
    public ApprovalDocumentType DocumentType { get; set; }
    public Guid DocumentId { get; set; }
    public ApprovalDecisionStatus Status { get; set; }
    public string RequestedById { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? DecidedById { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Comment { get; set; }
}
