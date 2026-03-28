namespace KipInventorySystem.Domain.Enums;

public enum TransferRequestStatus : int
{
    Draft = 1,
    PendingApproval,
    ChangesRequested,
    Approved,
    InTransit,
    Completed,
    Cancelled
}
