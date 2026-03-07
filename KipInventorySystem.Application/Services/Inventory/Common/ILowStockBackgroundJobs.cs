namespace KipInventorySystem.Application.Services.Inventory.Common;

public interface ILowStockBackgroundJobs
{
    Task EvaluateLowStockAsync(Guid warehouseId, Guid productId, CancellationToken cancellationToken = default);
}
