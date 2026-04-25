using KipInventorySystem.Application.Services.Inventory.GoodsReceipts.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.GoodsReceipts;

public interface IGoodsReceiptService
{
    Task<ServiceResponse<PurchaseOrderDTO>> ReceiveAsync(
        ReceiveGoodsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
