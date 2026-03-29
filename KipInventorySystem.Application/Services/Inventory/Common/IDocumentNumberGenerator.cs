namespace KipInventorySystem.Application.Services.Inventory.Common;

public interface IDocumentNumberGenerator
{
    string GeneratePurchaseOrderNumber();
    string GenerateTransferNumber();
    string GenerateAdjustmentNumber();
    string GenerateOpeningBalanceNumber();
    string GenerateSalesOrderNumber();
}
