using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class Customer : BaseEntity
{
    [Key]
    public Guid CustomerId { get; set; } = Guid.CreateVersion7();

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    public ICollection<SalesOrder> SalesOrders { get; set; } = [];
}
