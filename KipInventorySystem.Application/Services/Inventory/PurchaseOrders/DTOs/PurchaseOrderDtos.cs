using System.ComponentModel;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;

public class CreatePurchaseOrderDraftRequest
{
    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid SupplierId { get; set; }

    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid WarehouseId { get; set; }

    [DefaultValue("2026-03-15T00:00:00Z")]
    public DateTime? ExpectedArrivalDate { get; set; }

    [DefaultValue("Urgent restock for fast moving SKUs.")]
    public string? Notes { get; set; }

    public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = [];
}

public class UpdatePurchaseOrderDraftRequest
{
    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? SupplierId { get; set; }

    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid? WarehouseId { get; set; }

    [DefaultValue("2026-03-16T00:00:00Z")]
    public DateTime? ExpectedArrivalDate { get; set; }

    [DefaultValue("Updated ETA based on supplier confirmation.")]
    public string? Notes { get; set; }

    public List<CreatePurchaseOrderLineRequest>? Lines { get; set; }
}

public class CreatePurchaseOrderLineRequest
{
    [DefaultValue("7a708820-8f7e-42b2-86b0-97f703f46e6a")]
    public Guid ProductId { get; set; }

    [DefaultValue(50)]
    public int QuantityOrdered { get; set; }

    [DefaultValue(215000.00)]
    public decimal UnitCost { get; set; }
}

public class PurchaseOrderLineDTO
{
    public Guid PurchaseOrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseOrderDTO
{
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public DateTime OrderedAt { get; set; }
    public DateTime? ExpectedArrivalDate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineDTO> Lines { get; set; } = [];
}
