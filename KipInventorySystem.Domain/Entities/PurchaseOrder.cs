using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a purchase order made to a supplier for specific products. A purchase order is associated with a single supplier and a single warehouse where the products will be received. It contains multiple purchase order lines, each specifying a product, quantity, and unit price. The purchase order also tracks its status (e.g., Draft, Submitted, Received) and relevant dates such as when it was ordered and when it is expected to arrive. This entity is crucial for managing procurement processes and ensuring that inventory levels are maintained appropriately.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    [Key]
    public Guid PurchaseOrderId { get; set; } = Guid.CreateVersion7();

    [MaxLength(40)]
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = default!;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedArrivalDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = [];
}
