using System.ComponentModel.DataAnnotations;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Domain.Entities;

/// <summary>
/// Represents a transfer request entity in the inventory system. A transfer request is initiated to move stock from one warehouse to another. It includes details such as the source and destination warehouses, the status of the transfer, timestamps for when the transfer was requested and completed, and any relevant notes. Each transfer request can have multiple line items specifying the products and quantities to be transferred. This entity is crucial for managing inter-warehouse stock movements and ensuring accurate inventory tracking across locations.
/// </summary>
public class TransferRequest : BaseEntity
{
    [Key]
    public Guid TransferRequestId { get; set; } = Guid.CreateVersion7();

    [MaxLength(40)]
    public string TransferNumber { get; set; } = string.Empty;

    public Guid SourceWarehouseId { get; set; }
    public Warehouse SourceWarehouse { get; set; } = default!;

    public Guid DestinationWarehouseId { get; set; }
    public Warehouse DestinationWarehouse { get; set; } = default!;

    public TransferRequestStatus Status { get; set; } = TransferRequestStatus.Draft;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<TransferRequestLine> Lines { get; set; } = [];
}
