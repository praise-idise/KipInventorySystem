using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

public class ApprovalRequest : BaseEntity
{
    [Key]
    public Guid ApprovalRequestId { get; set; } = Guid.CreateVersion7();

    public ApprovalDocumentType DocumentType { get; set; }
    public Guid DocumentId { get; set; }
    public ApprovalDecisionStatus Status { get; set; } = ApprovalDecisionStatus.Pending;

    [MaxLength(128)]
    public string RequestedById { get; set; } = string.Empty;

    [MaxLength(256)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(128)]
    public string? DecidedById { get; set; }

    [MaxLength(256)]
    public string? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
