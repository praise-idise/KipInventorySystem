namespace KipInventorySystem.Domain.Enums;

public enum ApprovalDecisionStatus : int
{
    Pending = 1,
    Approved,
    ChangesRequested,
    Cancelled
}
