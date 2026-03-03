using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class StockAdjustmentLine : BaseEntity
{
    [Key]
    public Guid StockAdjustmentLineId { get; set; } = Guid.CreateVersion7();

    public Guid StockAdjustmentId { get; set; }
    public StockAdjustment StockAdjustment { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int Delta => QuantityAfter - QuantityBefore;
}
