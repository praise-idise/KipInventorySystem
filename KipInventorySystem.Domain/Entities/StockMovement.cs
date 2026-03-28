using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a movement of stock in or out of a warehouse. Stock movements can be associated with various operations such as receiving inventory, fulfilling orders, or transferring stock between warehouses. Each movement records the product, quantity, type of movement, and references to related entities for traceability.
/// </summary>
public class StockMovement : BaseEntity
{
    [Key]
    public Guid StockMovementId { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = default!;

    public StockMovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public StockMovementReferenceType? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    [MaxLength(256)]
    public string? Creator { get; set; }

    [MaxLength(128)]
    public string? CreatorId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
