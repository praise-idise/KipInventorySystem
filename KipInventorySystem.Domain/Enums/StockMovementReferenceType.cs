namespace KipInventorySystem.Domain.Enums;

public enum StockMovementReferenceType : int
{
    PurchaseOrder = 1,
    SalesOrder,
    TransferRequest,
    StockAdjustment,
    StockIssue
}
