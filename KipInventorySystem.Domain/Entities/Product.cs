using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a product that can be stocked in warehouses, ordered from suppliers, and tracked through inventory movements. Each product has a unique SKU, a name, an optional description, and unit of measure. The product also defines reorder thresholds and quantities to help manage inventory levels. Products can be linked to multiple suppliers for procurement, and can be associated with warehouse inventories, purchase order lines, and stock movements for comprehensive tracking.
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

    [MaxLength(3)]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(3)]
    public string BrandCode { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Brand { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ItemCode { get; set; } = string.Empty;

    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Pcs;

    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(30)]
    public string? Storage { get; set; }

    public ProductSize? Size { get; set; }

    [MaxLength(30)]
    public string? Dosage { get; set; }

    [MaxLength(30)]
    public string? Grade { get; set; }

    [MaxLength(30)]
    public string? Finish { get; set; }

    public int ReorderThreshold { get; set; } = 10;
    public int ReorderQuantity { get; set; } = 20;

    public ICollection<ProductSupplier> ProductSuppliers { get; set; } = [];
    public ICollection<WarehouseInventory> WarehouseInventories { get; set; } = [];
    public ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
