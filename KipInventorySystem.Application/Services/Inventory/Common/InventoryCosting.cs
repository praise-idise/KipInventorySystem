using KipInventorySystem.Domain.Entities;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public static class InventoryCosting
{
    private const int Scale = 4;

    public static decimal Round(decimal value)
        => Math.Round(value, Scale, MidpointRounding.AwayFromZero);

    public static (decimal UnitCost, decimal TotalCost) ApplyInbound(
        WarehouseInventory inventory,
        int quantity,
        decimal unitCost)
    {
        var roundedUnitCost = Round(unitCost);
        var totalCost = Round(roundedUnitCost * quantity);

        inventory.QuantityOnHand += quantity;
        inventory.InventoryValue = Round(inventory.InventoryValue + totalCost);
        inventory.AverageUnitCost = inventory.QuantityOnHand > 0
            ? Round(inventory.InventoryValue / inventory.QuantityOnHand)
            : 0m;

        return (roundedUnitCost, totalCost);
    }

    public static (decimal UnitCost, decimal TotalCost) ApplyOutbound(
        WarehouseInventory inventory,
        int quantity)
    {
        var roundedUnitCost = Round(inventory.AverageUnitCost);
        var totalCost = Round(roundedUnitCost * quantity);

        inventory.QuantityOnHand = Math.Max(0, inventory.QuantityOnHand - quantity);
        inventory.InventoryValue = Round(Math.Max(0m, inventory.InventoryValue - totalCost));

        if (inventory.QuantityOnHand == 0)
        {
            inventory.AverageUnitCost = 0m;
            inventory.InventoryValue = 0m;
        }
        else
        {
            inventory.AverageUnitCost = Round(inventory.InventoryValue / inventory.QuantityOnHand);
        }

        return (roundedUnitCost, totalCost);
    }
}
