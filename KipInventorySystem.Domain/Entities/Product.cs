using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a product that can be stocked in warehouses, ordered from suppliers, and tracked through inventory movements. Each product has a unique SKU, a name, an optional description, and unit of measure. The product also defines reorder thresholds and quantities to help manage inventory levels. Products can be associated with a default supplier for procurement purposes and can be linked to multiple warehouse inventories, purchase order lines, and stock movements for comprehensive tracking.
/// </summary>
public class Product : BaseEntity
{
    [Key]
    public Guid ProductId { get; set; } = Guid.CreateVersion7();

    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "pcs";

    public int ReorderThreshold { get; set; } = 10;
    public int ReorderQuantity { get; set; } = 20;

    public Guid? DefaultSupplierId { get; set; }
    public Supplier? DefaultSupplier { get; set; }

    public ICollection<WarehouseInventory> WarehouseInventories { get; set; } = [];
    public ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
