using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;

public class CreateProductSupplierRequest
{
    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid SupplierId { get; set; }

    [DefaultValue(215000.00)]
    public decimal UnitCost { get; set; }

    [DefaultValue(true)]
    public bool IsDefault { get; set; }
}

public class UpdateProductSupplierRequest
{
    [DefaultValue(215000.00)]
    public decimal UnitCost { get; set; }

    [DefaultValue(true)]
    public bool IsDefault { get; set; }
}

public class ProductSupplierDTO
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierEmail { get; set; }
    public string? SupplierPhone { get; set; }
    public string? SupplierContactPerson { get; set; }
    public int SupplierLeadTimeDays { get; set; }
    public decimal UnitCost { get; set; }
    public bool IsDefault { get; set; }
}
