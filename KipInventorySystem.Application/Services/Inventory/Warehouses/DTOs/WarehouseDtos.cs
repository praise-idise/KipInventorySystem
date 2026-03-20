using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;

public class CreateWarehouseRequest
{
    [DefaultValue("Lagos Central Warehouse")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("Lagos")]
    [MinLength(3)]
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

    [DefaultValue("Abuja")]
    [MinLength(3)]
    public string? State { get; set; }

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

public class WarehouseInventoryItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int? ReorderThresholdOverride { get; set; }
}
