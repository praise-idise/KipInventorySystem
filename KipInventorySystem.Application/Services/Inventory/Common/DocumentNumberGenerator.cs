namespace KipInventorySystem.Application.Services.Inventory.Common;

public class DocumentNumberGenerator : IDocumentNumberGenerator
{
    public string GeneratePurchaseOrderNumber() => Generate("PO");
    public string GenerateTransferNumber() => Generate("TR");
    public string GenerateAdjustmentNumber() => Generate("ADJ");

    private static string Generate(string prefix)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"{prefix}-{date}-{random}";
    }
}
