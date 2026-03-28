namespace KipInventorySystem.Domain.Enums;

public enum PurchaseOrderStatus : int
{
    Draft = 1,
    PendingApproval,
    ChangesRequested,
    Approved,
    PartiallyReceived,
    Received,
    Cancelled
}
