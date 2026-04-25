using KipInventorySystem.Application.Services.Inventory.StockIssues.DTOs;
using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.StockIssues;

public interface IStockIssueService
{
    Task<ServiceResponse<StockIssueResultDto>> IssueAsync(
        CreateStockIssueRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
