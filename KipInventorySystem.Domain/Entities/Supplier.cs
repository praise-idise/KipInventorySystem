using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class Supplier : BaseEntity
{
    [Key]
    public Guid SupplierId { get; set; } = Guid.CreateVersion7();

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(120)]
    public string? ContactPerson { get; set; }

    public int LeadTimeDays { get; set; } = 7;

    public ICollection<ProductSupplier> ProductSuppliers { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}
