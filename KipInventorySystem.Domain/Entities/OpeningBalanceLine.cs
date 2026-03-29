using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class OpeningBalanceLine : BaseEntity
{
    [Key]
    public Guid OpeningBalanceLineId { get; set; } = Guid.CreateVersion7();

    public Guid OpeningBalanceId { get; set; }
    public OpeningBalance OpeningBalance { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
