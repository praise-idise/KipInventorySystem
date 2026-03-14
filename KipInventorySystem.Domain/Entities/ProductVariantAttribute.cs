using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class ProductVariantAttribute
{
    [Key]
    public Guid ProductVariantAttributeId { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    [MaxLength(30)]
    public string AttributeName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string AttributeCode { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
