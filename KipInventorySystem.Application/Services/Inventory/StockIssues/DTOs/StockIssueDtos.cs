using System.ComponentModel;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.StockIssues.DTOs;

public class CreateStockIssueRequest
{
    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid WarehouseId { get; set; }

    [DefaultValue("Issued for same-day shipment.")]
    public string? Notes { get; set; }

    public List<StockIssueLineRequest> Lines { get; set; } = [];
}

public class StockIssueLineRequest
{
    [DefaultValue("7a708820-8f7e-42b2-86b0-97f703f46e6a")]
    public Guid ProductId { get; set; }

    [DefaultValue(5)]
    public int Quantity { get; set; }
}

public class StockIssueResultDto
{
    public Guid WarehouseId { get; set; }
    public List<StockIssueLineResultDto> Lines { get; set; } = [];
}

public class StockIssueLineResultDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public Guid StockMovementId { get; set; }
}
