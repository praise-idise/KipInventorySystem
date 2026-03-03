namespace KipInventorySystem.Domain.Enums;

public enum PurchaseOrderStatus : int
{
    Draft = 1,
    Submitted,
    PartiallyReceived,
    Received,
    Cancelled
}
