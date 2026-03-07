using KipInventorySystem.Application.Services.Inventory.GoodsReceipts.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.GoodsReceipts;

public interface IGoodsReceiptService
{
    Task<ServiceResponse<PurchaseOrderDto>> ReceiveAsync(
        ReceiveGoodsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
