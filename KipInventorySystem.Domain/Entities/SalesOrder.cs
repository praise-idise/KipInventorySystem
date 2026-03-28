using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

public class SalesOrder : BaseEntity
{
    [Key]
    public Guid SalesOrderId { get; set; } = Guid.CreateVersion7();

    [MaxLength(40)]
    public string SalesOrderNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = default!;

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = [];
}
