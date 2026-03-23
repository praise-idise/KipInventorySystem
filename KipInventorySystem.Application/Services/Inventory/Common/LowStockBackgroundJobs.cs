using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public class LowStockBackgroundJobs(
    IUnitOfWork unitOfWork,
    IDocumentNumberGenerator documentNumberGenerator,
    ILogger<LowStockBackgroundJobs> logger) : ILowStockBackgroundJobs
{
    public async Task EvaluateLowStockAsync(Guid warehouseId, Guid productId, CancellationToken cancellationToken = default)
    {
        var inventory = await unitOfWork.Repository<WarehouseInventory>()
            .FindAsync(x => x.WarehouseId == warehouseId && x.ProductId == productId, cancellationToken);

        if (inventory is null)
        {
            return;
        }

        var product = await unitOfWork.Repository<Product>()
            .GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return;
        }

        var threshold = inventory.ReorderThresholdOverride ?? product.ReorderThreshold;
        if (inventory.AvailableQuantity > threshold)
        {
            return;
        }

        logger.LogWarning(
            "Low stock detected. WarehouseId={WarehouseId}, ProductId={ProductId}, Available={Available}, Threshold={Threshold}",
            warehouseId,
            productId,
            inventory.AvailableQuantity,
            threshold);

        // TODO: Send email to procurement team for low stock alerts

        await EnsureWarehouseReorderDraftsAsync(warehouseId, cancellationToken);
    }

    private async Task EnsureWarehouseReorderDraftsAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
        var productRepo = unitOfWork.Repository<Product>();
        var productSupplierRepo = unitOfWork.Repository<ProductSupplier>();
        var poRepo = unitOfWork.Repository<PurchaseOrder>();
        var lineRepo = unitOfWork.Repository<PurchaseOrderLine>();

        // --- 1. Identify low stock products for this warehouse ---

        var inventories = await inventoryRepo.WhereAsync(x => x.WarehouseId == warehouseId, cancellationToken);
        if (inventories.Count == 0)
        {
            return;
        }

        var inventoryByProductId = inventories.ToDictionary(x => x.ProductId);
        var candidateProductIds = inventoryByProductId.Keys.ToList();

        var products = await productRepo.WhereAsync(
            x => candidateProductIds.Contains(x.ProductId),
            cancellationToken);

        if (products.Count == 0)
        {
            return;
        }

        var lowStockProducts = products
            .Where(product =>
            {
                if (!inventoryByProductId.TryGetValue(product.ProductId, out var inv))
                {
                    return false;
                }

                var threshold = inv.ReorderThresholdOverride ?? product.ReorderThreshold;
                return inv.AvailableQuantity <= threshold;
            })
            .ToList();

        if (lowStockProducts.Count == 0)
        {
            return;
        }

        var lowStockProductIds = lowStockProducts.Select(x => x.ProductId).ToList();

        // --- 2. Resolve default suppliers ---

        var defaultSupplierLinks = await productSupplierRepo.WhereAsync(
            x => x.IsDefault && lowStockProductIds.Contains(x.ProductId),
            cancellationToken);

        var defaultSupplierByProductId = defaultSupplierLinks
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First().SupplierId);

        foreach (var product in lowStockProducts.Where(x => !defaultSupplierByProductId.ContainsKey(x.ProductId)))
        {
            logger.LogWarning(
                "Low stock product requires manual procurement review — no default supplier. WarehouseId={WarehouseId}, ProductId={ProductId}, ProductName={ProductName}",
                warehouseId,
                product.ProductId,
                product.Name);

            // TODO: Send email to procurement team for products without default supplier
        }

        var reorderCandidates = lowStockProducts
            .Where(x => defaultSupplierByProductId.ContainsKey(x.ProductId))
            .ToList();

        if (reorderCandidates.Count == 0)
        {
            return;
        }

        // --- 3. Batch-fetch all open POs and their lines upfront ---

        var openStatuses = new[] { PurchaseOrderStatus.Draft, PurchaseOrderStatus.Submitted, PurchaseOrderStatus.PartiallyReceived };

        var openPurchaseOrders = await poRepo.WhereAsync(
            x => x.WarehouseId == warehouseId && openStatuses.Contains(x.Status),
            cancellationToken);

        var openPurchaseOrderIds = openPurchaseOrders.Select(x => x.PurchaseOrderId).ToHashSet();

        // Batch fetch all relevant open lines in one query instead of per-supplier inside the loop
        var allOpenLines = openPurchaseOrderIds.Count > 0
            ? await lineRepo.WhereAsync(
                x => openPurchaseOrderIds.Contains(x.PurchaseOrderId) && lowStockProductIds.Contains(x.ProductId),
                cancellationToken)
            : [];

        var outstandingByProductId = allOpenLines
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(line => Math.Max(0, line.QuantityOrdered - line.QuantityReceived)));

        // Batch fetch all draft lines for existing draft POs in one query
        var existingDraftIds = openPurchaseOrders
            .Where(x => x.Status == PurchaseOrderStatus.Draft)
            .Select(x => x.PurchaseOrderId)
            .ToHashSet();

        var allDraftLines = existingDraftIds.Count > 0
            ? await lineRepo.WhereAsync(
                x => existingDraftIds.Contains(x.PurchaseOrderId),
                cancellationToken)
            : [];

        var draftLinesByPoId = allDraftLines
            .GroupBy(x => x.PurchaseOrderId)
            .ToDictionary(x => x.Key, x => x.ToList());

        // --- 4. Process each supplier group ---

        var groupedBySupplier = reorderCandidates
            .GroupBy(x => defaultSupplierByProductId[x.ProductId])
            .ToList();

        foreach (var supplierGroup in groupedBySupplier)
        {
            var supplierId = supplierGroup.Key;

            var productsToReorder = supplierGroup
                .Where(x => x.ReorderQuantity > 0)
                .Where(x => !outstandingByProductId.TryGetValue(x.ProductId, out var outstandingQty) || outstandingQty <= 0)
                .ToList();

            foreach (var skipped in supplierGroup.Where(x => x.ReorderQuantity <= 0))
            {
                logger.LogWarning(
                    "Auto-reorder skipped — ReorderQuantity is not positive. ProductId={ProductId}, ReorderQuantity={ReorderQuantity}",
                    skipped.ProductId,
                    skipped.ReorderQuantity);
            }

            foreach (var skipped in supplierGroup.Where(x => outstandingByProductId.TryGetValue(x.ProductId, out var qty) && qty > 0))
            {
                logger.LogInformation(
                    "Auto-reorder skipped — open PO already has outstanding quantity. WarehouseId={WarehouseId}, SupplierId={SupplierId}, ProductId={ProductId}, Outstanding={Outstanding}",
                    warehouseId,
                    supplierId,
                    skipped.ProductId,
                    outstandingByProductId[skipped.ProductId]);
            }

            if (productsToReorder.Count == 0)
            {
                continue;
            }

            // Reuse existing draft or create a new one — one draft per supplier+warehouse
            var targetDraft = openPurchaseOrders
                .Where(x => x.SupplierId == supplierId && x.Status == PurchaseOrderStatus.Draft)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            var isNewDraft = targetDraft is null;

            if (targetDraft is null)
            {
                string poNumber;

                poNumber = await GenerateUniquePurchaseOrderNumberAsync(cancellationToken);

                targetDraft = new PurchaseOrder
                {
                    PurchaseOrderNumber = poNumber,
                    SupplierId = supplierId,
                    WarehouseId = warehouseId,
                    Status = PurchaseOrderStatus.Draft,
                    OrderedAt = DateTime.UtcNow,
                    Notes = "Auto-generated from low stock reorder check.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await poRepo.AddAsync(targetDraft, cancellationToken);
                openPurchaseOrders.Add(targetDraft);
                draftLinesByPoId[targetDraft.PurchaseOrderId] = [];
            }

            var draftLines = draftLinesByPoId.GetValueOrDefault(targetDraft.PurchaseOrderId, []);

            foreach (var product in productsToReorder)
            {
                var existingLine = draftLines.FirstOrDefault(x => x.ProductId == product.ProductId);
                if (existingLine is null)
                {
                    var newLine = new PurchaseOrderLine
                    {
                        PurchaseOrderId = targetDraft.PurchaseOrderId,
                        ProductId = product.ProductId,
                        QuantityOrdered = product.ReorderQuantity,
                        QuantityReceived = 0,
                        UnitCost = 0m, // Requires manual cost entry before submission
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    logger.LogWarning(
                        "Auto-reorder line created with zero unit cost — manual cost entry required before submission. ProductId={ProductId}, PurchaseOrderId={PurchaseOrderId}",
                        product.ProductId,
                        targetDraft.PurchaseOrderId);

                    await lineRepo.AddAsync(newLine, cancellationToken);
                    draftLines.Add(newLine);
                }
                else
                {
                    // Accumulate on existing draft line to avoid duplicate product lines in the same PO
                    existingLine.QuantityOrdered += product.ReorderQuantity;
                    existingLine.UpdatedAt = DateTime.UtcNow;
                    lineRepo.Update(existingLine);
                }
            }

            targetDraft.UpdatedAt = DateTime.UtcNow;
            poRepo.Update(targetDraft);

            logger.LogInformation(
                "Auto-reorder draft purchase order {Action}. WarehouseId={WarehouseId}, SupplierId={SupplierId}, PurchaseOrderId={PurchaseOrderId}, ProductCount={ProductCount}",
                isNewDraft ? "created" : "updated",
                warehouseId,
                supplierId,
                targetDraft.PurchaseOrderId,
                productsToReorder.Count);
        }

        // Single save after all supplier groups are processed — avoids partial commit on failure
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateUniquePurchaseOrderNumberAsync(CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<PurchaseOrder>();

        for (var i = 0; i < 5; i++)
        {
            var number = documentNumberGenerator.GeneratePurchaseOrderNumber();
            var exists = await repo.ExistsAsync(x => x.PurchaseOrderNumber == number, cancellationToken);
            if (!exists)
            {
                return number;
            }
        }

        throw new InvalidOperationException("Unable to generate unique purchase order number for low stock reorder after 5 attempts.");
    }
}