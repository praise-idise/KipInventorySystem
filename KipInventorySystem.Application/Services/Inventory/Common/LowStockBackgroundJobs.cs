using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public class LowStockBackgroundJobs(
    IUnitOfWork unitOfWork,
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
        if (inventory.AvailableQuantity <= threshold)
        {
            // TODO: When reorder flow is introduced, group low-stock products by ProductSupplier.IsDefault
            // and flag products that do not have a default supplier link for manual procurement review.
            logger.LogWarning(
                "Low stock detected. WarehouseId={WarehouseId}, ProductId={ProductId}, Available={Available}, Threshold={Threshold}",
                warehouseId,
                productId,
                inventory.AvailableQuantity,
                threshold);
        }
    }
}
