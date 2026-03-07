using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;

public class CreateWarehouseRequest
{
    [DefaultValue("Lagos Central Warehouse")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("Lagos")]
    public string State { get; set; } = string.Empty;

    [DefaultValue("Ikeja, Lagos")]
    public string? Location { get; set; }

    [DefaultValue(25000)]
    public int CapacityUnits { get; set; }
}

public class UpdateWarehouseRequest
{
    [DefaultValue("Lagos Central Warehouse")]
    public string? Name { get; set; }

    [DefaultValue("Ikeja, Lagos")]
    public string? Location { get; set; }

    [DefaultValue(30000)]
    public int? CapacityUnits { get; set; }

    [DefaultValue(true)]
    public bool? IsActive { get; set; }
}

public class WarehouseDto
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int CapacityUnits { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
