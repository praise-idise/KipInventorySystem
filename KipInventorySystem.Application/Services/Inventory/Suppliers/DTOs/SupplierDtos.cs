using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;

public class CreateSupplierRequest
{
    [DefaultValue("Kip Core Supplies Ltd")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("procurement@kipcore.com")]
    public string? Email { get; set; }

    [DefaultValue("+2348012345678")]
    public string? Phone { get; set; }

    [DefaultValue("Amina Yusuf")]
    public string? ContactPerson { get; set; }

    [DefaultValue(7)]
    public int LeadTimeDays { get; set; } = 7;
}

public class UpdateSupplierRequest
{
    [DefaultValue("Kip Core Supplies Ltd")]
    public string? Name { get; set; }

    [DefaultValue("supply@kipcore.com")]
    public string? Email { get; set; }

    [DefaultValue("+2348098765432")]
    public string? Phone { get; set; }

    [DefaultValue("Amina Yusuf")]
    public string? ContactPerson { get; set; }

    [DefaultValue(10)]
    public int? LeadTimeDays { get; set; }

    [DefaultValue(true)]
    public bool? IsActive { get; set; }
}

public class SupplierDto
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
