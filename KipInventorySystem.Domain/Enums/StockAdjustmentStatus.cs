namespace KipInventorySystem.Domain.Enums;

public enum StockAdjustmentStatus : int
{
    Draft = 1,
    Submitted,
    Approved,
    Applied,
    Cancelled
}
