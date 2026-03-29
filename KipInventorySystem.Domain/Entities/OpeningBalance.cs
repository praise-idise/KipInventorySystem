using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class OpeningBalance : BaseEntity
{
    [Key]
    public Guid OpeningBalanceId { get; set; } = Guid.CreateVersion7();

    [MaxLength(40)]
    public string OpeningBalanceNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = default!;

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<OpeningBalanceLine> Lines { get; set; } = [];
}
