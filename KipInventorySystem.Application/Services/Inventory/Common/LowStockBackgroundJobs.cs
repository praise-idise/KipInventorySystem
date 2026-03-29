using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public class LowStockBackgroundJobs(
    IUnitOfWork unitOfWork,
    IDocumentNumberGenerator documentNumberGenerator,
    UserManager<ApplicationUser> userManager,
    IEmailBackgroundJobs emailBackgroundJobs,
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

        var warehouse = await unitOfWork.Repository<Warehouse>()
            .GetByIdAsync(warehouseId, cancellationToken);

        if (warehouse is null)
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

        await SendLowStockAlertNotificationsAsync(
            warehouse,
            product,
            inventory.AvailableQuantity,
            threshold,
            product.ReorderQuantity,
            cancellationToken);

        await EnsureWarehouseReorderDraftsAsync(warehouseId, cancellationToken);
    }

    private async Task EnsureWarehouseReorderDraftsAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
        var productRepo = unitOfWork.Repository<Product>();
        var productSupplierRepo = unitOfWork.Repository<ProductSupplier>();
        var poRepo = unitOfWork.Repository<PurchaseOrder>();
        var lineRepo = unitOfWork.Repository<PurchaseOrderLine>();

        // 1. Identify low-stock products for this warehouse.
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

        // 2. Resolve default suppliers.
        var defaultSupplierLinks = await productSupplierRepo.WhereAsync(
            x => x.IsDefault && lowStockProductIds.Contains(x.ProductId),
            cancellationToken);

        var defaultSupplierByProductId = defaultSupplierLinks
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var product in lowStockProducts.Where(x => !defaultSupplierByProductId.ContainsKey(x.ProductId)))
        {
            var inventory = inventoryByProductId[product.ProductId];
            var threshold = inventory.ReorderThresholdOverride ?? product.ReorderThreshold;

            logger.LogWarning(
                "Low stock product requires manual procurement review - no default supplier. WarehouseId={WarehouseId}, ProductId={ProductId}, ProductName={ProductName}",
                warehouseId,
                product.ProductId,
                product.Name);

            await SendManualProcurementReviewNotificationsAsync(
                warehouseId,
                product,
                inventory.AvailableQuantity,
                threshold,
                cancellationToken);
        }

        var reorderCandidates = lowStockProducts
            .Where(x => defaultSupplierByProductId.ContainsKey(x.ProductId))
            .ToList();

        if (reorderCandidates.Count == 0)
        {
            return;
        }

        // 3. Batch-fetch all open POs and their lines up front.
        var openStatuses = new[]
        {
            PurchaseOrderStatus.Draft,
            PurchaseOrderStatus.PendingApproval,
            PurchaseOrderStatus.Approved,
            PurchaseOrderStatus.PartiallyReceived
        };

        var openPurchaseOrders = await poRepo.WhereAsync(
            x => x.WarehouseId == warehouseId && openStatuses.Contains(x.Status),
            cancellationToken);

        var openPurchaseOrderIds = openPurchaseOrders.Select(x => x.PurchaseOrderId).ToHashSet();

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

        // 4. Process each supplier group.
        var groupedBySupplier = reorderCandidates
            .GroupBy(x => defaultSupplierByProductId[x.ProductId].SupplierId)
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
                    "Auto-reorder skipped - ReorderQuantity is not positive. ProductId={ProductId}, ReorderQuantity={ReorderQuantity}",
                    skipped.ProductId,
                    skipped.ReorderQuantity);
            }

            foreach (var skipped in supplierGroup.Where(x => outstandingByProductId.TryGetValue(x.ProductId, out var qty) && qty > 0))
            {
                logger.LogInformation(
                    "Auto-reorder skipped - open PO already has outstanding quantity. WarehouseId={WarehouseId}, SupplierId={SupplierId}, ProductId={ProductId}, Outstanding={Outstanding}",
                    warehouseId,
                    supplierId,
                    skipped.ProductId,
                    outstandingByProductId[skipped.ProductId]);
            }

            if (productsToReorder.Count == 0)
            {
                continue;
            }

            var productsWithValidCost = new List<Product>();
            foreach (var product in productsToReorder)
            {
                var supplierLink = defaultSupplierByProductId[product.ProductId];
                if (supplierLink.UnitCost <= 0)
                {
                    logger.LogWarning(
                        "Auto-reorder skipped - default supplier unit cost is not positive. WarehouseId={WarehouseId}, SupplierId={SupplierId}, ProductId={ProductId}, UnitCost={UnitCost}",
                        warehouseId,
                        supplierId,
                        product.ProductId,
                        supplierLink.UnitCost);
                    continue;
                }

                productsWithValidCost.Add(product);
            }

            if (productsWithValidCost.Count == 0)
            {
                continue;
            }

            // Reuse existing draft or create a new one - one draft per supplier+warehouse.
            var targetDraft = openPurchaseOrders
                .Where(x => x.SupplierId == supplierId && x.Status == PurchaseOrderStatus.Draft)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            var isNewDraft = targetDraft is null;
            if (targetDraft is null)
            {
                var poNumber = await GenerateUniquePurchaseOrderNumberAsync(cancellationToken);

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
            foreach (var product in productsWithValidCost)
            {
                var supplierLink = defaultSupplierByProductId[product.ProductId];
                var existingLine = draftLines.FirstOrDefault(x => x.ProductId == product.ProductId);
                if (existingLine is null)
                {
                    var newLine = new PurchaseOrderLine
                    {
                        PurchaseOrderId = targetDraft.PurchaseOrderId,
                        ProductId = product.ProductId,
                        QuantityOrdered = product.ReorderQuantity,
                        QuantityReceived = 0,
                        UnitCost = supplierLink.UnitCost,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await lineRepo.AddAsync(newLine, cancellationToken);
                    draftLines.Add(newLine);
                }
                else
                {
                    // Accumulate on existing draft line to avoid duplicate product lines in the same PO.
                    existingLine.QuantityOrdered += product.ReorderQuantity;
                    if (existingLine.UnitCost <= 0)
                    {
                        existingLine.UnitCost = supplierLink.UnitCost;
                    }

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
                productsWithValidCost.Count);
        }

        // Single save after all supplier groups are processed.
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

    private async Task SendLowStockAlertNotificationsAsync(
        Warehouse warehouse,
        Product product,
        int availableQuantity,
        int threshold,
        int reorderQuantity,
        CancellationToken cancellationToken)
    {
        var recipients = await GetNotificationRecipientsAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Low stock alert skipped because no active admin or procurement recipients were found. WarehouseId={WarehouseId}, ProductId={ProductId}",
                warehouse.WarehouseId,
                product.ProductId);
            return;
        }

        foreach (var recipient in recipients)
        {
            try
            {
                await emailBackgroundJobs.SendLowStockAlertEmailAsync(
                    recipient.Email!,
                    FormatRecipientName(recipient),
                    warehouse.Name,
                    warehouse.Code,
                    product.Name,
                    product.Sku,
                    availableQuantity,
                    threshold,
                    reorderQuantity,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send low stock notification. WarehouseId={WarehouseId}, ProductId={ProductId}, RecipientUserId={RecipientUserId}",
                    warehouse.WarehouseId,
                    product.ProductId,
                    recipient.Id);
            }
        }
    }

    private async Task SendManualProcurementReviewNotificationsAsync(
        Guid warehouseId,
        Product product,
        int availableQuantity,
        int threshold,
        CancellationToken cancellationToken)
    {
        var warehouse = await unitOfWork.Repository<Warehouse>().GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return;
        }

        var recipients = await GetNotificationRecipientsAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Manual procurement review alert skipped because no active admin or procurement recipients were found. WarehouseId={WarehouseId}, ProductId={ProductId}",
                warehouseId,
                product.ProductId);
            return;
        }

        foreach (var recipient in recipients)
        {
            try
            {
                await emailBackgroundJobs.SendManualProcurementReviewEmailAsync(
                    recipient.Email!,
                    FormatRecipientName(recipient),
                    warehouse.Name,
                    warehouse.Code,
                    product.Name,
                    product.Sku,
                    availableQuantity,
                    threshold,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send manual procurement review notification. WarehouseId={WarehouseId}, ProductId={ProductId}, RecipientUserId={RecipientUserId}",
                    warehouseId,
                    product.ProductId,
                    recipient.Id);
            }
        }
    }

    private async Task<List<ApplicationUser>> GetNotificationRecipientsAsync(CancellationToken cancellationToken)
    {
        var procurementUsers = await userManager.GetUsersInRoleAsync(ROLE_TYPE.PROCUREMENT_OFFICER.ToString());
        cancellationToken.ThrowIfCancellationRequested();

        var adminUsers = await userManager.GetUsersInRoleAsync(ROLE_TYPE.ADMIN.ToString());
        cancellationToken.ThrowIfCancellationRequested();

        return procurementUsers
            .Concat(adminUsers)
            .Where(x => x.IsActive && !x.IsDeleted && !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
    }

    private static string FormatRecipientName(ApplicationUser user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user.UserName ?? "Team";
    }
}
