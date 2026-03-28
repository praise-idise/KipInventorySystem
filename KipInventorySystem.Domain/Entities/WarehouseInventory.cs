using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents the inventory of a specific product at a specific warehouse. This entity tracks the quantity on hand, reserved quantity, and allows for an optional reorder threshold override. It serves as a junction between the Warehouse and Product entities, enabling the system to manage stock levels and availability across multiple locations.
/// </summary>
public class WarehouseInventory : BaseEntity
{
    [Key]
    public Guid WarehouseInventoryId { get; set; } = Guid.CreateVersion7();

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int QuantityOnHand { get; set; }
    public int ReservedQuantity { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal InventoryValue { get; set; }
    public int? ReorderThresholdOverride { get; set; }

    public int AvailableQuantity => QuantityOnHand - ReservedQuantity;
}
