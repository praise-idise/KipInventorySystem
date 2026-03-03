namespace KipInventorySystem.Domain.Enums;

public enum StockMovementType : int
{
    Receipt = 1,
    Issue,
    AdjustmentIncrease,
    AdjustmentDecrease,
    TransferIn,
    TransferOut
}
