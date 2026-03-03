namespace KipInventorySystem.Domain.Enums;

public enum AdjustmentReason : int
{
    CountCorrection = 1,
    Damage,
    Expiry,
    Loss,
    FoundStock,
    WriteOff
}
