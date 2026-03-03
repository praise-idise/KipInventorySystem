using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a stock adjustment operation in the inventory system. A stock adjustment is performed to correct inventory discrepancies, account for damaged goods, or reflect physical counts. This entity captures the details of the adjustment, including the warehouse where the adjustment is made, the reason for the adjustment, its status (e.g., draft, applied), and timestamps for when the adjustment was requested and applied. Each stock adjustment can have multiple line items specifying the products and quantities being adjusted. This allows for accurate tracking and auditing of inventory changes that are not related to regular stock movements like receipts or issues.
/// </summary>
public class StockAdjustment : BaseEntity
{
    [Key]
    public Guid StockAdjustmentId { get; set; } = Guid.CreateVersion7();

    [MaxLength(40)]
    public string AdjustmentNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = default!;

    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Draft;
    public AdjustmentReason Reason { get; set; } = AdjustmentReason.CountCorrection;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<StockAdjustmentLine> Lines { get; set; } = [];
}
