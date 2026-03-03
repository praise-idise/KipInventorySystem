using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a physical location where inventory is stored. Warehouses can have multiple inventory items and can be associated with purchase orders and stock movements.
/// </summary>
public class Warehouse : BaseEntity
{
    [Key]
    public Guid WarehouseId { get; set; } = Guid.CreateVersion7();

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Location { get; set; }

    public int CapacityUnits { get; set; }

    public ICollection<WarehouseInventory> InventoryItems { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
