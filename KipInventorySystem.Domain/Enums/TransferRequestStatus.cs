namespace KipInventorySystem.Domain.Enums;

public enum TransferRequestStatus : int
{
    Draft = 1,
    Submitted,
    InTransit,
    Completed,
    Cancelled
}
