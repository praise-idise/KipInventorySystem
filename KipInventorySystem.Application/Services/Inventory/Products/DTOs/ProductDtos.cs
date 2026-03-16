using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace KipInventorySystem.Application.Services.Inventory.Products.DTOs;

public class CreateProductVariantAttributeDTO
{
    [DefaultValue("Color")]
    public string AttributeName { get; set; } = string.Empty;

    [DefaultValue("BLK")]
    public string AttributeCode { get; set; } = string.Empty;

    [DefaultValue(1)]
    public int SortOrder { get; set; }
}

public class ProductVariantAttributeDTO
{
    public string AttributeName { get; set; } = string.Empty;
    public string AttributeCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateProductDTO
{
    [DefaultValue("ELE")]
    public string CategoryCode { get; set; } = string.Empty;

    [DefaultValue("APL")]
    public string BrandCode { get; set; } = string.Empty;

    public List<CreateProductVariantAttributeDTO> VariantAttributes { get; set; } = [];

    [DefaultValue("iPhone 15 128GB Black")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("Fast moving smartphone SKU.")]
    public string? Description { get; set; }

    [DefaultValue("pcs")]
    public string UnitOfMeasure { get; set; } = "pcs";

    [DefaultValue(20)]
    public int ReorderThreshold { get; set; } = 10;

    [DefaultValue(100)]
    public int ReorderQuantity { get; set; } = 40;
}

public class UpdateProductDTO
{
    [DefaultValue("ELE")]
    public string? CategoryCode { get; set; }

    [DefaultValue("APL")]
    public string? BrandCode { get; set; }

    public List<ProductVariantAttributeDTO>? VariantAttributes { get; set; }

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
    [DefaultValue(true)]
    public bool? IsActive { get; set; }
}

public class ProductDTO
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string BrandCode { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public List<ProductVariantAttributeDTO> VariantAttributes { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProductSupplierDTO>? Suppliers { get; set; }
    public int ReorderThreshold { get; set; }
    public int ReorderQuantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
