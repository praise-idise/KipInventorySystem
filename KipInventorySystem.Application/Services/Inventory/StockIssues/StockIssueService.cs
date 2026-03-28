using Hangfire;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.StockIssues.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Responses;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.StockIssues;

public class StockIssueService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IUserContext userContext,
    ILogger<StockIssueService> logger) : IStockIssueService
{
    public async Task<ServiceResponse<StockIssueResultDto>> IssueAsync(
        CreateStockIssueRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProductIds = new HashSet<Guid>();

        var response = await idempotencyService.ExecuteAsync(
            "stock-issue-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("stockIssue.create", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var movementCreatorId = currentUser.UserId;
                var movementCreator = currentUser.FullName;

                var warehouseRepo = unitOfWork.Repository<Warehouse>();
                var productRepo = unitOfWork.Repository<Product>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var warehouse = await warehouseRepo.GetByIdAsync(request.WarehouseId, token);
                if (warehouse is null)
                {
                    return ServiceResponse<StockIssueResultDto>.BadRequest("Warehouse was not found.");
                }

                foreach (var line in request.Lines)
                {
                    var product = await productRepo.GetByIdAsync(line.ProductId, token);
                    if (product is null)
                    {
                        return ServiceResponse<StockIssueResultDto>.BadRequest($"Product '{line.ProductId}' was not found.");
                    }
                }

                var movements = new List<StockMovement>();
                var lineResults = new List<StockIssueLineResultDto>();
                var notes = BuildMovementNotes(request.Reason, request.Notes);

                foreach (var line in request.Lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == request.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        return ServiceResponse<StockIssueResultDto>.Conflict(
                            $"No inventory record exists for product '{line.ProductId}' in warehouse '{request.WarehouseId}'.");
                    }

                    if (inventory.AvailableQuantity < line.Quantity)
                    {
                        return ServiceResponse<StockIssueResultDto>.Conflict(
                            $"Insufficient available stock for product '{line.ProductId}'. Available={inventory.AvailableQuantity}, requested={line.Quantity}.");
                    }

                    var (unitCost, totalCost) = InventoryCosting.ApplyOutbound(inventory, line.Quantity);
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProductIds.Add(line.ProductId);

                    var movement = new StockMovement
                    {
                        ProductId = line.ProductId,
                        WarehouseId = request.WarehouseId,
                        MovementType = StockMovementType.Issue,
                        Quantity = line.Quantity,
                        UnitCost = unitCost,
                        TotalCost = totalCost,
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.StockIssue,
                        ReferenceId = null,
                        Creator = movementCreator,
                        CreatorId = movementCreatorId,
                        Notes = notes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    movements.Add(movement);
                    lineResults.Add(new StockIssueLineResultDto
                    {
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        StockMovementId = movement.StockMovementId
                    });
                }

                await movementRepo.AddRangeAsync(movements, token);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockIssue, warehouseId={WarehouseId}, movementCount={MovementCount}, referenceType={ReferenceType}, referenceId={ReferenceId}",
                    "CreateStockIssue",
                    movementCreatorId,
                    request.WarehouseId,
                    movements.Count,
                    StockMovementReferenceType.StockIssue,
                    null);

                return ServiceResponse<StockIssueResultDto>.Success(new StockIssueResultDto
                {
                    WarehouseId = request.WarehouseId,
                    Reason = request.Reason,
                    Lines = lineResults
                }, "Stock issue completed.");
            }, token),
            cancellationToken);

        if (response.Succeeded)
        {
            foreach (var productId in affectedProductIds)
            {
                BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                    "default",
                    jobs => jobs.EvaluateLowStockAsync(request.WarehouseId, productId, default));
            }
        }

        return response;
    }

    private static string BuildMovementNotes(StockIssueReason reason, string? notes)
    {
        var reasonText = $"Reason: {reason}";
        if (string.IsNullOrWhiteSpace(notes))
        {
            return reasonText;
        }

        return $"{reasonText}. {notes.Trim()}";
    }
}
