using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Inventory.GoodsReceipts.DTOs;

public class ReceiveGoodsRequest
{
    [DefaultValue("4b2b3e27-d574-4532-9ec9-b683fc70f145")]
    public Guid PurchaseOrderId { get; set; }

    [DefaultValue("Truck arrived at 10:15AM, goods verified.")]
    public string? Notes { get; set; }

    public List<ReceiveGoodsLineRequest> Lines { get; set; } = [];
}

public class ReceiveGoodsLineRequest
{
    [DefaultValue("8fd14a4d-a10a-48d3-a1b0-52a8d1a09179")]
    public Guid PurchaseOrderLineId { get; set; }

    [DefaultValue(20)]
    public int QuantityReceivedNow { get; set; }
}
