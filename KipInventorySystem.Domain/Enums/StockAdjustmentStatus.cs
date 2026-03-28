namespace KipInventorySystem.Domain.Enums;

public enum StockAdjustmentStatus : int
{
    Draft = 1,
    PendingApproval,
    ChangesRequested,
    Approved,
    Applied,
    Cancelled
}
