using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using System.ComponentModel;
using System.Text.Json.Serialization;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.Products.DTOs;

public class CreateProductDTO
{
    [DefaultValue("ELE")]
    public string CategoryCode { get; set; } = string.Empty;

    [DefaultValue("Apple")]
    public string Brand { get; set; } = string.Empty;

    [DefaultValue("iPhone 15 128GB Black")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("Fast moving smartphone SKU.")]
    public string? Description { get; set; }

    [DefaultValue("Pcs")]
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Pcs;

    [DefaultValue("Black")]
    public string? Color { get; set; }

    [DefaultValue("128GB")]
    public string? Storage { get; set; }

    [DefaultValue("M")]
    public ProductSize? Size { get; set; }

    [DefaultValue("500MG")]
    public string? Dosage { get; set; }

    [DefaultValue("Grade A")]
    public string? Grade { get; set; }

    [DefaultValue("Gloss")]
    public string? Finish { get; set; }

    [DefaultValue(20)]
    public int ReorderThreshold { get; set; } = 10;

    [DefaultValue(100)]
    public int ReorderQuantity { get; set; } = 40;
}

public class UpdateProductDTO
{
    [DefaultValue("ELE")]
    public string? CategoryCode { get; set; }

    [DefaultValue("Apple")]
    public string? Brand { get; set; }

    [DefaultValue("iPhone 15 128GB Black")]
    public string? Name { get; set; }

    [DefaultValue("Fast moving smartphone SKU.")]
    public string? Description { get; set; }

    [DefaultValue("Pcs")]
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    [DefaultValue("Black")]
    public string? Color { get; set; }

    [DefaultValue("128GB")]
    public string? Storage { get; set; }

    [DefaultValue("M")]
    public ProductSize? Size { get; set; }

    [DefaultValue("500MG")]
    public string? Dosage { get; set; }

    [DefaultValue("Grade A")]
    public string? Grade { get; set; }

    [DefaultValue("Gloss")]
    public string? Finish { get; set; }

    [DefaultValue(20)]
    public int? ReorderThreshold { get; set; }

    [DefaultValue(100)]
    public int? ReorderQuantity { get; set; }
    [DefaultValue(true)]
    public bool? IsActive { get; set; }
}

public class ProductDTO
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public string? Color { get; set; }
    public string? Storage { get; set; }
    public ProductSize? Size { get; set; }
    public string? Dosage { get; set; }
    public string? Grade { get; set; }
    public string? Finish { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProductSupplierDTO>? Suppliers { get; set; }
    public int ReorderThreshold { get; set; }
    public int ReorderQuantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
