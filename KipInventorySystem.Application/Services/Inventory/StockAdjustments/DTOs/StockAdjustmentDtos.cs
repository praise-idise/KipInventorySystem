using System.ComponentModel;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;

public class CreateStockAdjustmentDraftRequest
{
    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid WarehouseId { get; set; }

    [DefaultValue(AdjustmentReason.CountCorrection)]
    public AdjustmentReason Reason { get; set; } = AdjustmentReason.CountCorrection;

    [DefaultValue("Cycle count reconciliation after stock take.")]
    public string? Notes { get; set; }

    public List<CreateStockAdjustmentLineRequest> Lines { get; set; } = [];
}

public class CreateStockAdjustmentLineRequest
{
    [DefaultValue("7a708820-8f7e-42b2-86b0-97f703f46e6a")]
    public Guid ProductId { get; set; }

    [DefaultValue(95)]
    public int QuantityAfter { get; set; }

    [DefaultValue(215000.00)]
    public decimal? UnitCost { get; set; }
}

public class StockAdjustmentLineDto
{
    public Guid StockAdjustmentLineId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public decimal? UnitCost { get; set; }
    public int Delta { get; set; }
}

public class StockAdjustmentDto
{
    public Guid StockAdjustmentId { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public StockAdjustmentStatus Status { get; set; }
    public AdjustmentReason Reason { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? Notes { get; set; }
    public List<StockAdjustmentLineDto> Lines { get; set; } = [];
}
