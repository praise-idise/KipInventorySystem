using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.GoodsReceipts.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
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
    public Task<ServiceResponse<PurchaseOrderDTO>> ReceiveAsync(
        ReceiveGoodsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "goods-receipt-receive",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("goodsReceipt.receive", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var movementCreatorId = currentUser.UserId;
                var movementCreator = currentUser.FullName;

                var poRepo = unitOfWork.Repository<PurchaseOrder>();
                var lineRepo = unitOfWork.Repository<PurchaseOrderLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();
                
                var po = await poRepo.GetByIdAsync(request.PurchaseOrderId, token);
                if (po is null)
                {
                    return ServiceResponse<PurchaseOrderDTO>.NotFound("Purchase order was not found.");
                }

                if (po.Status is not PurchaseOrderStatus.Approved and not PurchaseOrderStatus.PartiallyReceived)
                {
                    return ServiceResponse<PurchaseOrderDTO>.Conflict(
                        "Goods can only be received for approved or partially received purchase orders.");
                }

                var poLines = await lineRepo.WhereAsync(x => x.PurchaseOrderId == request.PurchaseOrderId, token);
                
                if (poLines.Count == 0)
                {
                    return ServiceResponse<PurchaseOrderDTO>.BadRequest("Purchase order has no lines.");
                }

                var requestLinesById = request.Lines.ToDictionary(x => x.PurchaseOrderLineId, x => x);
                var movements = new List<StockMovement>();

                // Validate the requested receipt quantities before making any stock changes.
                foreach (var requestLine in request.Lines)
                {
                    var poLine = poLines.FirstOrDefault(x => x.PurchaseOrderLineId == requestLine.PurchaseOrderLineId);
                    if (poLine is null)
                    {
                        return ServiceResponse<PurchaseOrderDTO>.BadRequest(
                            $"Purchase order line '{requestLine.PurchaseOrderLineId}' was not found.");
                    }

                    var remaining = poLine.QuantityOrdered - poLine.QuantityReceived;
                    if (requestLine.QuantityReceivedNow > remaining)
                    {
                        return ServiceResponse<PurchaseOrderDTO>.BadRequest(
                            $"Cannot receive {requestLine.QuantityReceivedNow} units for line '{poLine.PurchaseOrderLineId}'. Remaining quantity is {remaining}.");
                    }
                }

                // Apply each requested receipt line to both the PO and warehouse stock.
                foreach (var poLine in poLines)
                {
                    if (!requestLinesById.TryGetValue(poLine.PurchaseOrderLineId, out var requestLine))
                    {
                        continue;
                    }

                    if (poLine.UnitCost <= 0)
                    {
                        return ServiceResponse<PurchaseOrderDTO>.Conflict(
                            $"Purchase order line '{poLine.PurchaseOrderLineId}' has invalid unit cost. Please update the PO line cost before receiving goods.");
                    }

                    poLine.QuantityReceived += requestLine.QuantityReceivedNow;
                    poLine.UpdatedAt = DateTime.UtcNow;
                    lineRepo.Update(poLine);

                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == po.WarehouseId && x.ProductId == poLine.ProductId,
                        token);

                    if (inventory is null)
                    {
                        var unitCost = InventoryCosting.Round(poLine.UnitCost);
                        var totalCost = InventoryCosting.Round(unitCost * requestLine.QuantityReceivedNow);
                        inventory = new WarehouseInventory
                        {
                            WarehouseId = po.WarehouseId,
                            ProductId = poLine.ProductId,
                            QuantityOnHand = requestLine.QuantityReceivedNow,
                            ReservedQuantity = 0,
                            AverageUnitCost = unitCost,
                            InventoryValue = totalCost,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await inventoryRepo.AddAsync(inventory, token);
                        movements.Add(new StockMovement
                        {
                            ProductId = poLine.ProductId,
                            WarehouseId = po.WarehouseId,
                            MovementType = StockMovementType.Receipt,
                            Quantity = requestLine.QuantityReceivedNow,
                            UnitCost = unitCost,
                            TotalCost = totalCost,
                            OccurredAt = DateTime.UtcNow,
                            ReferenceType = StockMovementReferenceType.PurchaseOrder,
                            ReferenceId = po.PurchaseOrderId,
                            Creator = movementCreator,
                            CreatorId = movementCreatorId,
                            Notes = request.Notes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        var (unitCost, totalCost) = InventoryCosting.ApplyInbound(
                            inventory,
                            requestLine.QuantityReceivedNow,
                            poLine.UnitCost);
                        inventory.UpdatedAt = DateTime.UtcNow;
                        inventoryRepo.Update(inventory);

                        // Record each physical receipt as a stock movement for audit/history.
                        movements.Add(new StockMovement
                        {
                            ProductId = poLine.ProductId,
                            WarehouseId = po.WarehouseId,
                            MovementType = StockMovementType.Receipt,
                            Quantity = requestLine.QuantityReceivedNow,
                            UnitCost = unitCost,
                            TotalCost = totalCost,
                            OccurredAt = DateTime.UtcNow,
                            ReferenceType = StockMovementReferenceType.PurchaseOrder,
                            ReferenceId = po.PurchaseOrderId,
                            Creator = movementCreator,
                            CreatorId = movementCreatorId,
                            Notes = request.Notes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (movements.Count > 0)
                {
                    await movementRepo.AddRangeAsync(movements, token);
                }

                // The PO is complete only when every line has been fully received.
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

                return ServiceResponse<PurchaseOrderDTO>.Success(
                    mapper.Map<PurchaseOrderDTO>(po),
                    "Goods receipt applied successfully.");
            }, token),
            cancellationToken);
    }
}
