using Hangfire;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.StockAdjustments;

public class StockAdjustmentService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IDocumentNumberGenerator documentNumberGenerator,
    IUserContext userContext,
    IMapper mapper,
    ILogger<StockAdjustmentService> logger) : IStockAdjustmentService
{
    public Task<ServiceResponse<StockAdjustmentDto>> CreateDraftAsync(
        CreateStockAdjustmentDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync<CreateStockAdjustmentDraftRequest, StockAdjustmentDto>(
            "stock-adjustment-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.createDraft", async _ =>
            {
                var warehouseRepo = unitOfWork.Repository<Warehouse>();
                var productRepo = unitOfWork.Repository<Product>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();

                var warehouse = await warehouseRepo.GetByIdAsync(request.WarehouseId, token);
                if (warehouse is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.BadRequest("Warehouse was not found.");
                }

                if (request.Lines.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
                {
                    return ServiceResponse<StockAdjustmentDto>.BadRequest("Duplicate products are not allowed in stock adjustment lines.");
                }

                foreach (var line in request.Lines)
                {
                    var product = await productRepo.GetByIdAsync(line.ProductId, token);
                    if (product is null)
                    {
                        return ServiceResponse<StockAdjustmentDto>.BadRequest($"Product '{line.ProductId}' was not found.");
                    }
                }

                var adjustment = mapper.Map<StockAdjustment>(request);
                adjustment.AdjustmentNumber = await GenerateUniqueAdjustmentNumberAsync(token);
                adjustment.Status = StockAdjustmentStatus.Draft;
                adjustment.RequestedAt = DateTime.UtcNow;
                adjustment.CreatedAt = DateTime.UtcNow;
                adjustment.UpdatedAt = DateTime.UtcNow;

                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
                await adjustmentRepo.AddAsync(adjustment, token);

                var lines = new List<StockAdjustmentLine>();
                foreach (var lineRequest in request.Lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == request.WarehouseId && x.ProductId == lineRequest.ProductId,
                        token);

                    var quantityBefore = inventory?.QuantityOnHand ?? 0;
                    var line = mapper.Map<StockAdjustmentLine>(lineRequest);
                    line.StockAdjustmentId = adjustment.StockAdjustmentId;
                    line.QuantityBefore = quantityBefore;
                    line.CreatedAt = DateTime.UtcNow;
                    line.UpdatedAt = DateTime.UtcNow;
                    lines.Add(line);
                }

                await lineRepo.AddRangeAsync(lines, token);
                adjustment.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, status={Status}",
                    "CreateStockAdjustmentDraft",
                    userContext.GetCurrentUser().UserId,
                    adjustment.StockAdjustmentId,
                    adjustment.Status);

                return ServiceResponse<StockAdjustmentDto>.Created(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    "Stock adjustment draft created.");
            }, token),
            cancellationToken);
    }

    public Task<ServiceResponse<StockAdjustmentDto>> SubmitAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(
            stockAdjustmentId,
            idempotencyKey,
            "stock-adjustment-submit",
            "stockAdjustment.submit",
            fromStatus: StockAdjustmentStatus.Draft,
            toStatus: StockAdjustmentStatus.Submitted,
            successMessage: "Stock adjustment submitted.",
            cancellationToken);
    }

    public Task<ServiceResponse<StockAdjustmentDto>> ApproveAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return TransitionAsync(
            stockAdjustmentId,
            idempotencyKey,
            "stock-adjustment-approve",
            "stockAdjustment.approve",
            fromStatus: StockAdjustmentStatus.Submitted,
            toStatus: StockAdjustmentStatus.Approved,
            successMessage: "Stock adjustment approved.",
            cancellationToken);
    }

    public async Task<ServiceResponse<StockAdjustmentDto>> ApplyAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync<Guid, StockAdjustmentDto>(
            "stock-adjustment-apply",
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.apply", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var movementCreatorId = currentUser.UserId;
                var movementCreator = currentUser.FullName;

                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var adjustment = await adjustmentRepo.GetByIdAsync(stockAdjustmentId, token);
                if (adjustment is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.NotFound("Stock adjustment was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.StockAdjustmentId == stockAdjustmentId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<StockAdjustmentDto>.BadRequest("Stock adjustment has no lines.");
                }

                if (adjustment.Status == StockAdjustmentStatus.Applied)
                {
                    adjustment.Lines = lines;
                    return ServiceResponse<StockAdjustmentDto>.Success(
                        mapper.Map<StockAdjustmentDto>(adjustment),
                        "Stock adjustment already applied.");
                }

                if (adjustment.Status != StockAdjustmentStatus.Approved)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("Only approved stock adjustments can be applied.");
                }

                var movements = new List<StockMovement>();
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == adjustment.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    var currentQuantity = inventory?.QuantityOnHand ?? 0;
                    if (currentQuantity != line.QuantityBefore)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Stock changed for product '{line.ProductId}' since draft creation. Expected {line.QuantityBefore}, current {currentQuantity}.");
                    }
                }

                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == adjustment.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        inventory = new WarehouseInventory
                        {
                            WarehouseId = adjustment.WarehouseId,
                            ProductId = line.ProductId,
                            QuantityOnHand = line.QuantityAfter,
                            ReservedQuantity = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await inventoryRepo.AddAsync(inventory, token);
                    }

                    var delta = line.QuantityAfter - line.QuantityBefore;
                    if (inventory.QuantityOnHand != line.QuantityAfter)
                    {
                        inventory.QuantityOnHand = line.QuantityAfter;
                        inventory.UpdatedAt = DateTime.UtcNow;
                        inventoryRepo.Update(inventory);
                    }
                    affectedProducts.Add(line.ProductId);

                    if (delta != 0)
                    {
                        movements.Add(new StockMovement
                        {
                            ProductId = line.ProductId,
                            WarehouseId = adjustment.WarehouseId,
                            MovementType = delta > 0 ? StockMovementType.AdjustmentIncrease : StockMovementType.AdjustmentDecrease,
                            Quantity = Math.Abs(delta),
                            OccurredAt = DateTime.UtcNow,
                            ReferenceType = StockMovementReferenceType.StockAdjustment,
                            ReferenceId = adjustment.StockAdjustmentId,
                            Creator = movementCreator,
                            CreatorId = movementCreatorId,
                            Notes = adjustment.Notes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (movements.Count > 0)
                {
                    await movementRepo.AddRangeAsync(movements, token);
                }

                adjustment.Status = StockAdjustmentStatus.Applied;
                adjustment.AppliedAt = DateTime.UtcNow;
                adjustment.UpdatedAt = DateTime.UtcNow;
                adjustmentRepo.Update(adjustment);
                adjustment.Lines = lines;
                affectedWarehouse = adjustment.WarehouseId;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, oldStatus={OldStatus}, newStatus={NewStatus}",
                    "ApplyStockAdjustment",
                    userContext.GetCurrentUser().UserId,
                    adjustment.StockAdjustmentId,
                    StockAdjustmentStatus.Approved,
                    adjustment.Status);

                return ServiceResponse<StockAdjustmentDto>.Success(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    "Stock adjustment applied.");
            }, token),
            cancellationToken);

        if (response.Succeeded && affectedWarehouse.HasValue)
        {
            foreach (var productId in affectedProducts)
            {
                BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                    "default",
                    jobs => jobs.EvaluateLowStockAsync(affectedWarehouse.Value, productId, default));
            }
        }

        return response;
    }

    public Task<ServiceResponse<StockAdjustmentDto>> CancelAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync<Guid, StockAdjustmentDto>(
            "stock-adjustment-cancel",
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.cancel", async _ =>
            {
                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();

                var adjustment = await adjustmentRepo.GetByIdAsync(stockAdjustmentId, token);
                if (adjustment is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.NotFound("Stock adjustment was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.StockAdjustmentId == stockAdjustmentId, token);

                if (adjustment.Status == StockAdjustmentStatus.Cancelled)
                {
                    adjustment.Lines = lines;
                    return ServiceResponse<StockAdjustmentDto>.Success(
                        mapper.Map<StockAdjustmentDto>(adjustment),
                        "Stock adjustment already cancelled.");
                }

                if (adjustment.Status == StockAdjustmentStatus.Applied)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("Applied stock adjustments cannot be cancelled.");
                }

                adjustment.Status = StockAdjustmentStatus.Cancelled;
                adjustment.UpdatedAt = DateTime.UtcNow;
                adjustmentRepo.Update(adjustment);
                adjustment.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, newStatus={NewStatus}",
                    "CancelStockAdjustment",
                    userContext.GetCurrentUser().UserId,
                    adjustment.StockAdjustmentId,
                    adjustment.Status);

                return ServiceResponse<StockAdjustmentDto>.Success(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    "Stock adjustment cancelled.");
            }, token),
            cancellationToken);
    }

    public async Task<ServiceResponse<StockAdjustmentDto>> GetByIdAsync(Guid stockAdjustmentId, CancellationToken cancellationToken = default)
    {
        var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
        var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
        var adjustment = await adjustmentRepo.GetByIdAsync(stockAdjustmentId, cancellationToken);
        if (adjustment is null)
        {
            return ServiceResponse<StockAdjustmentDto>.NotFound("Stock adjustment was not found.");
        }

        adjustment.Lines = await lineRepo.WhereAsync(x => x.StockAdjustmentId == stockAdjustmentId, cancellationToken);
        return ServiceResponse<StockAdjustmentDto>.Success(mapper.Map<StockAdjustmentDto>(adjustment));
    }

    public async Task<ServiceResponse<PaginationResult<StockAdjustmentDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
        var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
        var pagedAdjustments = await adjustmentRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            cancellationToken: cancellationToken);

        var adjustments = pagedAdjustments.Records.ToList();
        if (adjustments.Count > 0)
        {
            var ids = adjustments.Select(x => x.StockAdjustmentId).ToHashSet();
            var lines = await lineRepo.WhereAsync(x => ids.Contains(x.StockAdjustmentId), cancellationToken);
            var grouped = lines.GroupBy(x => x.StockAdjustmentId).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var adjustment in adjustments)
            {
                if (grouped.TryGetValue(adjustment.StockAdjustmentId, out var adjustmentLines))
                {
                    adjustment.Lines = adjustmentLines;
                }
            }
        }

        var response = new PaginationResult<StockAdjustmentDto>
        {
            Records = adjustments.Select(x => mapper.Map<StockAdjustmentDto>(x)).ToList(),
            TotalRecords = pagedAdjustments.TotalRecords,
            PageSize = pagedAdjustments.PageSize,
            CurrentPage = pagedAdjustments.CurrentPage
        };

        return ServiceResponse<PaginationResult<StockAdjustmentDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<StockAdjustmentDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
        var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
        var pagedAdjustments = await adjustmentRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            x => x.AdjustmentNumber.ToLower().Contains(term) ||
                 (x.Notes != null && x.Notes.ToLower().Contains(term)),
            cancellationToken);

        var adjustments = pagedAdjustments.Records.ToList();
        if (adjustments.Count > 0)
        {
            var ids = adjustments.Select(x => x.StockAdjustmentId).ToHashSet();
            var lines = await lineRepo.WhereAsync(x => ids.Contains(x.StockAdjustmentId), cancellationToken);
            var grouped = lines.GroupBy(x => x.StockAdjustmentId).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var adjustment in adjustments)
            {
                if (grouped.TryGetValue(adjustment.StockAdjustmentId, out var adjustmentLines))
                {
                    adjustment.Lines = adjustmentLines;
                }
            }
        }

        var response = new PaginationResult<StockAdjustmentDto>
        {
            Records = adjustments.Select(x => mapper.Map<StockAdjustmentDto>(x)).ToList(),
            TotalRecords = pagedAdjustments.TotalRecords,
            PageSize = pagedAdjustments.PageSize,
            CurrentPage = pagedAdjustments.CurrentPage
        };

        return ServiceResponse<PaginationResult<StockAdjustmentDto>>.Success(response);
    }

    private Task<ServiceResponse<StockAdjustmentDto>> TransitionAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        string idempotencyOperationName,
        string transactionOperationName,
        StockAdjustmentStatus fromStatus,
        StockAdjustmentStatus toStatus,
        string successMessage,
        CancellationToken cancellationToken)
    {
        return idempotencyService.ExecuteAsync<Guid, StockAdjustmentDto>(
            idempotencyOperationName,
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync(transactionOperationName, async _ =>
            {
                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();

                var adjustment = await adjustmentRepo.GetByIdAsync(stockAdjustmentId, token);
                if (adjustment is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.NotFound("Stock adjustment was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.StockAdjustmentId == stockAdjustmentId, token);
                if (adjustment.Status == toStatus || adjustment.Status == StockAdjustmentStatus.Applied)
                {
                    adjustment.Lines = lines;
                    return ServiceResponse<StockAdjustmentDto>.Success(
                        mapper.Map<StockAdjustmentDto>(adjustment),
                        successMessage);
                }

                if (adjustment.Status != fromStatus)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict(
                        $"Invalid transition from '{adjustment.Status}' to '{toStatus}'.");
                }

                adjustment.Status = toStatus;
                adjustment.UpdatedAt = DateTime.UtcNow;
                adjustmentRepo.Update(adjustment);
                adjustment.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, oldStatus={OldStatus}, newStatus={NewStatus}",
                    transactionOperationName,
                    userContext.GetCurrentUser().UserId,
                    adjustment.StockAdjustmentId,
                    fromStatus,
                    toStatus);

                return ServiceResponse<StockAdjustmentDto>.Success(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    successMessage);
            }, token),
            cancellationToken);
    }

    private async Task<string> GenerateUniqueAdjustmentNumberAsync(CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<StockAdjustment>();
        for (var i = 0; i < 5; i++)
        {
            var number = documentNumberGenerator.GenerateAdjustmentNumber();
            var exists = await repo.ExistsAsync(x => x.AdjustmentNumber == number, cancellationToken);
            if (!exists)
            {
                return number;
            }
        }

        throw new InvalidOperationException("Unable to generate unique adjustment number.");
    }

}
