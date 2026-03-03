using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a line item within a purchase order, detailing the specific product being ordered, the quantity, and the unit cost. Each purchase order line is associated with a single purchase order and a single product. This entity is essential for tracking the details of what is being procured, how much is being ordered, and at what cost, allowing for accurate inventory management and financial tracking.
/// </summary>
public class PurchaseOrderLine : BaseEntity
{
    [Key]
    public Guid PurchaseOrderLineId { get; set; } = Guid.CreateVersion7();

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
}
