using System.ComponentModel;
using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.SalesOrders.DTOs;

public class CreateSalesOrderDraftRequest
{
    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? CustomerId { get; set; }

    public CreateCustomerRequest? Customer { get; set; }

    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid WarehouseId { get; set; }

    [DefaultValue("Customer requested same-day pickup.")]
    public string? Notes { get; set; }

    public List<CreateSalesOrderLineRequest> Lines { get; set; } = [];
}

public class UpdateSalesOrderDraftRequest
{
    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? CustomerId { get; set; }

    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid? WarehouseId { get; set; }

    [DefaultValue("Customer moved pickup to tomorrow morning.")]
    public string? Notes { get; set; }

    public List<CreateSalesOrderLineRequest>? Lines { get; set; }
}

public class CreateSalesOrderLineRequest
{
    [DefaultValue("7a708820-8f7e-42b2-86b0-97f703f46e6a")]
    public Guid ProductId { get; set; }

    [DefaultValue(3)]
    public int QuantityOrdered { get; set; }

    [DefaultValue(250000.00)]
    public decimal UnitPrice { get; set; }
}

public class FulfillSalesOrderRequest
{
    [DefaultValue("Customer picked remaining items.")]
    public string? Notes { get; set; }

    public List<FulfillSalesOrderLineRequest> Lines { get; set; } = [];
}

public class FulfillSalesOrderLineRequest
{
    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid SalesOrderLineId { get; set; }

    [DefaultValue(2)]
    public int QuantityFulfilledNow { get; set; }
}

public class SalesOrderLineDto
{
    public Guid SalesOrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityFulfilled { get; set; }
    public decimal UnitPrice { get; set; }
}

public class SalesOrderDto
{
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid WarehouseId { get; set; }
    public SalesOrderStatus Status { get; set; }
    public DateTime OrderedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public string? Notes { get; set; }
    public List<SalesOrderLineDto> Lines { get; set; } = [];
}
