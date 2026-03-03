using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class SalesOrderLine : BaseEntity
{
    [Key]
    public Guid SalesOrderLineId { get; set; } = Guid.CreateVersion7();

    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int QuantityOrdered { get; set; }
    public int QuantityFulfilled { get; set; }
    public decimal UnitPrice { get; set; }
}
