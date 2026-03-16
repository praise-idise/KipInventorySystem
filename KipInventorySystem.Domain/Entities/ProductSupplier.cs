namespace KipInventorySystem.Domain.Entities;

public class ProductSupplier
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public bool IsDefault { get; set; }
}
