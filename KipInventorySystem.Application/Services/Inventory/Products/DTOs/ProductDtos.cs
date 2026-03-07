using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Inventory.Products.DTOs;

public class CreateProductRequest
{
    [DefaultValue("ELE")]
    public string CategoryCode { get; set; } = string.Empty;

    [DefaultValue("APL")]
    public string BrandCode { get; set; } = string.Empty;

    [DefaultValue("BLK")]
    public string VariantCode { get; set; } = string.Empty;

    [DefaultValue("iPhone 15 128GB Black")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("Fast moving smartphone SKU.")]
    public string? Description { get; set; }

    [DefaultValue("pcs")]
    public string UnitOfMeasure { get; set; } = "pcs";

    [DefaultValue(20)]
    public int ReorderThreshold { get; set; } = 10;

    [DefaultValue(100)]
    public int ReorderQuantity { get; set; } = 20;

    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? DefaultSupplierId { get; set; }
}

public class UpdateProductRequest
{
    [DefaultValue("iPhone 15 128GB Black")]
    public string? Name { get; set; }

    [DefaultValue("Fast moving smartphone SKU.")]
    public string? Description { get; set; }

    [DefaultValue("pcs")]
    public string? UnitOfMeasure { get; set; }

    [DefaultValue(20)]
    public int? ReorderThreshold { get; set; }

    [DefaultValue(100)]
    public int? ReorderQuantity { get; set; }

    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? DefaultSupplierId { get; set; }

    [DefaultValue(true)]
    public bool? IsActive { get; set; }
}

public class ProductDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public int ReorderThreshold { get; set; }
    public int ReorderQuantity { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
