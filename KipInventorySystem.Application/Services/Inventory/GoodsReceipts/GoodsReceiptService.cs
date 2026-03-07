using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.GoodsReceipts.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.GoodsReceipts;

public class GoodsReceiptService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IUserContext userContext,
    IMapper mapper,
    ILogger<GoodsReceiptService> logger) : IGoodsReceiptService
{
    public Task<ServiceResponse<PurchaseOrderDto>> ReceiveAsync(
        ReceiveGoodsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync<ReceiveGoodsRequest, PurchaseOrderDto>(
            "goods-receipt-receive",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("goodsReceipt.receive", async _ =>
            {
                var poRepo = unitOfWork.Repository<PurchaseOrder>();
                var lineRepo = unitOfWork.Repository<PurchaseOrderLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var po = await poRepo.GetByIdAsync(request.PurchaseOrderId, token);
                if (po is null)
                {
                    return ServiceResponse<PurchaseOrderDto>.NotFound("Purchase order was not found.");
                }

                if (po.Status is not PurchaseOrderStatus.Submitted and not PurchaseOrderStatus.PartiallyReceived)
                {
                    return ServiceResponse<PurchaseOrderDto>.Conflict(
                        "Goods can only be received for submitted or partially received purchase orders.");
                }

                var poLines = await lineRepo.WhereAsync(x => x.PurchaseOrderId == request.PurchaseOrderId, token);
                if (poLines.Count == 0)
                {
                    return ServiceResponse<PurchaseOrderDto>.BadRequest("Purchase order has no lines.");
                }

                var requestLinesById = request.Lines.ToDictionary(x => x.PurchaseOrderLineId, x => x);
                var movements = new List<StockMovement>();

                foreach (var requestLine in request.Lines)
                {
                    var poLine = poLines.FirstOrDefault(x => x.PurchaseOrderLineId == requestLine.PurchaseOrderLineId);
                    if (poLine is null)
                    {
                        return ServiceResponse<PurchaseOrderDto>.BadRequest(
                            $"Purchase order line '{requestLine.PurchaseOrderLineId}' was not found.");
                    }

                    var remaining = poLine.QuantityOrdered - poLine.QuantityReceived;
                    if (requestLine.QuantityReceivedNow > remaining)
                    {
                        return ServiceResponse<PurchaseOrderDto>.BadRequest(
                            $"Cannot receive {requestLine.QuantityReceivedNow} units for line '{poLine.PurchaseOrderLineId}'. Remaining quantity is {remaining}.");
                    }
                }

                foreach (var poLine in poLines)
                {
                    if (!requestLinesById.TryGetValue(poLine.PurchaseOrderLineId, out var requestLine))
                    {
                        continue;
                    }

                    poLine.QuantityReceived += requestLine.QuantityReceivedNow;
                    poLine.UpdatedAt = DateTime.UtcNow;
                    lineRepo.Update(poLine);

                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == po.WarehouseId && x.ProductId == poLine.ProductId,
                        token);

                    if (inventory is null)
                    {
                        inventory = new WarehouseInventory
                        {
                            WarehouseId = po.WarehouseId,
                            ProductId = poLine.ProductId,
                            QuantityOnHand = 0,
                            ReservedQuantity = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await inventoryRepo.AddAsync(inventory, token);
                    }

                    inventory.QuantityOnHand += requestLine.QuantityReceivedNow;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);

                    movements.Add(new StockMovement
                    {
                        ProductId = poLine.ProductId,
                        WarehouseId = po.WarehouseId,
                        MovementType = StockMovementType.Receipt,
                        Quantity = requestLine.QuantityReceivedNow,
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.PurchaseOrder,
                        ReferenceId = po.PurchaseOrderId,
                        Notes = request.Notes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                if (movements.Count > 0)
                {
                    await movementRepo.AddRangeAsync(movements, token);
                }

                po.Status = poLines.All(x => x.QuantityReceived >= x.QuantityOrdered)
                    ? PurchaseOrderStatus.Received
                    : PurchaseOrderStatus.PartiallyReceived;
                po.UpdatedAt = DateTime.UtcNow;
                poRepo.Update(po);
                po.Lines = poLines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=PurchaseOrder, entityId={EntityId}, status={Status}, movementCount={MovementCount}",
                    "ReceiveGoods",
                    userContext.GetCurrentUser().UserId,
                    po.PurchaseOrderId,
                    po.Status,
                    movements.Count);

                return ServiceResponse<PurchaseOrderDto>.Success(
                    mapper.Map<PurchaseOrderDto>(po),
                    "Goods receipt applied successfully.");
            }, token),
            cancellationToken);
    }
}
