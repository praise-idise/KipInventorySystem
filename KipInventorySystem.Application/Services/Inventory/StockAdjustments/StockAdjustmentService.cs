using Hangfire;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
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
        return idempotencyService.ExecuteAsync(
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

                // Each product should appear once so every line has a single final target quantity.
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
                // Save the header first so the generated id can be assigned to each line.
                await adjustmentRepo.AddAsync(adjustment, token);

                var lines = new List<StockAdjustmentLine>();
                foreach (var lineRequest in request.Lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == request.WarehouseId && x.ProductId == lineRequest.ProductId,
                        token);

                    if (inventory is null)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"No inventory record exists for product '{lineRequest.ProductId}' in warehouse '{request.WarehouseId}'. Use opening balance or goods receipt instead.");
                    }

                    // Capture the draft-time quantity so apply can detect later stock drift.
                    var quantityBefore = inventory.QuantityOnHand;
                    // A provided unit cost must be a real inbound valuation, never zero or negative.
                    if (lineRequest.UnitCost.HasValue && lineRequest.UnitCost.Value <= 0)
                    {
                        return ServiceResponse<StockAdjustmentDto>.BadRequest(
                            $"Unit cost must be greater than zero for product '{lineRequest.ProductId}'.");
                    }

                    // Manual unit cost only makes sense when this adjustment adds stock.
                    if (lineRequest.UnitCost.HasValue && lineRequest.QuantityAfter <= quantityBefore)
                    {
                        return ServiceResponse<StockAdjustmentDto>.BadRequest(
                            $"Unit cost is only allowed for upward adjustments. Product '{lineRequest.ProductId}' does not increase stock.");
                    }

                    var line = mapper.Map<StockAdjustmentLine>(lineRequest);
                    line.StockAdjustmentId = adjustment.StockAdjustmentId;
                    line.QuantityBefore = quantityBefore;
                    line.UnitCost = lineRequest.UnitCost.HasValue
                        ? InventoryCosting.Round(lineRequest.UnitCost.Value)
                        : null;
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
        return idempotencyService.ExecuteAsync(
            "stock-adjustment-submit",
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.submit", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

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

                // Treat a repeated submit as success so callers can safely retry.
                if (adjustment.Status == StockAdjustmentStatus.PendingApproval)
                {
                    adjustment.Lines = lines;
                    return ServiceResponse<StockAdjustmentDto>.Success(
                        mapper.Map<StockAdjustmentDto>(adjustment),
                        "Stock adjustment is already awaiting approval.");
                }

                if (adjustment.Status is StockAdjustmentStatus.Approved or StockAdjustmentStatus.Applied)
                {
                    adjustment.Lines = lines;
                    return ServiceResponse<StockAdjustmentDto>.Success(
                        mapper.Map<StockAdjustmentDto>(adjustment),
                        "Stock adjustment is already approved.");
                }

                if (adjustment.Status is not StockAdjustmentStatus.Draft and not StockAdjustmentStatus.ChangesRequested)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("Only draft or returned stock adjustments can be submitted for approval.");
                }

                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.StockAdjustment, adjustment.StockAdjustmentId, token);
                if (pendingApproval is not null)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("This stock adjustment already has a pending approval request.");
                }

                // Create a new approval record for the current submission cycle.
                adjustment.Status = StockAdjustmentStatus.PendingApproval;
                adjustment.UpdatedAt = DateTime.UtcNow;
                adjustmentRepo.Update(adjustment);
                adjustment.Lines = lines;

                await approvalRepo.AddAsync(new ApprovalRequest
                {
                    DocumentType = ApprovalDocumentType.StockAdjustment,
                    DocumentId = adjustment.StockAdjustmentId,
                    Status = ApprovalDecisionStatus.Pending,
                    RequestedById = currentUser.UserId,
                    RequestedBy = currentUser.FullName,
                    RequestedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, token);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, newStatus={NewStatus}",
                    "SubmitStockAdjustmentForApproval",
                    currentUser.UserId,
                    adjustment.StockAdjustmentId,
                    adjustment.Status);

                return ServiceResponse<StockAdjustmentDto>.Success(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    "Stock adjustment submitted for approval.");
            }, token),
            cancellationToken);
    }

    public Task<ServiceResponse<StockAdjustmentDto>> ApproveAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "stock-adjustment-approve",
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.approve", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

                var adjustment = await adjustmentRepo.GetByIdAsync(stockAdjustmentId, token);
                if (adjustment is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.NotFound("Stock adjustment was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.StockAdjustmentId == stockAdjustmentId, token);
                adjustment.Lines = lines;

                if (adjustment.Status == StockAdjustmentStatus.Approved)
                {
                    return ServiceResponse<StockAdjustmentDto>.Success(
                        mapper.Map<StockAdjustmentDto>(adjustment),
                        "Stock adjustment already approved.");
                }

                if (adjustment.Status != StockAdjustmentStatus.PendingApproval)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("Only stock adjustments awaiting approval can be approved.");
                }

                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.StockAdjustment, adjustment.StockAdjustmentId, token);
                if (pendingApproval is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("No pending approval request was found for this stock adjustment.");
                }

                // Keep the document status and approval decision in sync.
                adjustment.Status = StockAdjustmentStatus.Approved;
                adjustment.UpdatedAt = DateTime.UtcNow;
                adjustmentRepo.Update(adjustment);

                pendingApproval.Status = ApprovalDecisionStatus.Approved;
                pendingApproval.DecidedById = currentUser.UserId;
                pendingApproval.DecidedBy = currentUser.FullName;
                pendingApproval.DecidedAt = DateTime.UtcNow;
                pendingApproval.UpdatedAt = DateTime.UtcNow;
                approvalRepo.Update(pendingApproval);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, newStatus={NewStatus}",
                    "ApproveStockAdjustment",
                    currentUser.UserId,
                    adjustment.StockAdjustmentId,
                    adjustment.Status);

                return ServiceResponse<StockAdjustmentDto>.Success(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    "Stock adjustment approved.");
            }, token),
            cancellationToken);
    }

    public async Task<ServiceResponse<StockAdjustmentDto>> ApplyAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync(
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
                // Confirm live stock still matches the draft snapshot before applying any changes.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == adjustment.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Inventory record for product '{line.ProductId}' no longer exists in warehouse '{adjustment.WarehouseId}'.");
                    }

                    var currentQuantity = inventory.QuantityOnHand;

                    if (currentQuantity != line.QuantityBefore)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Stock for product '{line.ProductId}' changed since this adjustment was drafted. " +
                            $"Drafted at {line.QuantityBefore} units, currently {currentQuantity} units. " +
                            $"Please cancel and re-draft to reflect current stock levels.");
                    }
                }

                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == adjustment.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Inventory record for product '{line.ProductId}' no longer exists in warehouse '{adjustment.WarehouseId}'.");
                    }

                    var delta = line.QuantityAfter - line.QuantityBefore;
                    // Re-validate persisted draft data before mutating inventory balances and value.
                    if (line.UnitCost.HasValue && line.UnitCost.Value <= 0)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Unit cost must be greater than zero for product '{line.ProductId}'.");
                    }

                    // Downward adjustments use the existing inventory cost, not a new inbound unit cost.
                    if (line.UnitCost.HasValue && delta <= 0)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Unit cost is only allowed for upward adjustments. Product '{line.ProductId}' does not increase stock.");
                    }

                    if (delta > 0)
                    {
                        var inboundUnitCost = line.UnitCost.HasValue
                            ? InventoryCosting.Round(line.UnitCost.Value)
                            : InventoryCosting.Round(inventory.AverageUnitCost);

                        if (!line.UnitCost.HasValue && inboundUnitCost <= 0)
                        {
                            logger.LogWarning(
                                "Stock adjustment increase used zero fallback average cost. WarehouseId={WarehouseId}, ProductId={ProductId}",
                                adjustment.WarehouseId,
                                line.ProductId);
                        }

                        var (appliedUnitCost, totalCost) = InventoryCosting.ApplyInbound(inventory, delta, inboundUnitCost);
                        movements.Add(new StockMovement
                        {
                            ProductId = line.ProductId,
                            WarehouseId = adjustment.WarehouseId,
                            MovementType = StockMovementType.AdjustmentIncrease,
                            Quantity = delta,
                            UnitCost = appliedUnitCost,
                            TotalCost = totalCost,
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
                    else if (delta < 0)
                    {
                        var decreaseQuantity = Math.Abs(delta);
                        if (inventory.QuantityOnHand < decreaseQuantity)
                        {
                            return ServiceResponse<StockAdjustmentDto>.Conflict(
                                $"Insufficient stock to apply downward adjustment for product '{line.ProductId}'.");
                        }

                        var (appliedUnitCost, totalCost) = InventoryCosting.ApplyOutbound(inventory, decreaseQuantity);
                        movements.Add(new StockMovement
                        {
                            ProductId = line.ProductId,
                            WarehouseId = adjustment.WarehouseId,
                            MovementType = StockMovementType.AdjustmentDecrease,
                            Quantity = decreaseQuantity,
                            UnitCost = appliedUnitCost,
                            TotalCost = totalCost,
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

                    // The costing helpers should leave on-hand quantity exactly at the requested final count.
                    if (inventory.QuantityOnHand != line.QuantityAfter)
                    {
                        return ServiceResponse<StockAdjustmentDto>.Conflict(
                            $"Calculated quantity mismatch for product '{line.ProductId}'. Expected {line.QuantityAfter}, computed {inventory.QuantityOnHand}.");
                    }

                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProducts.Add(line.ProductId);
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
            // Re-evaluate alerts after stock levels change without blocking the main request.
            foreach (var productId in affectedProducts)
            {
                BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                    "default",
                    jobs => jobs.EvaluateLowStockAsync(affectedWarehouse.Value, productId, default));
            }
        }

        return response;
    }

    public Task<ServiceResponse<StockAdjustmentDto>> ReturnForChangesAsync(
        Guid stockAdjustmentId,
        ApprovalDecisionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "stock-adjustment-return",
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.returnForChanges", async _ =>
            {
                // Require actionable feedback so the requester knows what to fix before resubmitting.
                if (string.IsNullOrWhiteSpace(request.Comment))
                {
                    return ServiceResponse<StockAdjustmentDto>.BadRequest("A comment is required when returning a stock adjustment for changes.");
                }

                var currentUser = userContext.GetCurrentUser();
                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

                var adjustment = await adjustmentRepo.GetByIdAsync(stockAdjustmentId, token);
                if (adjustment is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.NotFound("Stock adjustment was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.StockAdjustmentId == stockAdjustmentId, token);
                adjustment.Lines = lines;

                if (adjustment.Status != StockAdjustmentStatus.PendingApproval)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("Only stock adjustments awaiting approval can be returned for changes.");
                }

                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.StockAdjustment, adjustment.StockAdjustmentId, token);
                if (pendingApproval is null)
                {
                    return ServiceResponse<StockAdjustmentDto>.Conflict("No pending approval request was found for this stock adjustment.");
                }

                adjustment.Status = StockAdjustmentStatus.ChangesRequested;
                adjustment.UpdatedAt = DateTime.UtcNow;
                adjustmentRepo.Update(adjustment);

                pendingApproval.Status = ApprovalDecisionStatus.ChangesRequested;
                pendingApproval.Comment = request.Comment.Trim();
                pendingApproval.DecidedById = currentUser.UserId;
                pendingApproval.DecidedBy = currentUser.FullName;
                pendingApproval.DecidedAt = DateTime.UtcNow;
                pendingApproval.UpdatedAt = DateTime.UtcNow;
                approvalRepo.Update(pendingApproval);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=StockAdjustment, entityId={EntityId}, newStatus={NewStatus}",
                    "ReturnStockAdjustmentForChanges",
                    currentUser.UserId,
                    adjustment.StockAdjustmentId,
                    adjustment.Status);

                return ServiceResponse<StockAdjustmentDto>.Success(
                    mapper.Map<StockAdjustmentDto>(adjustment),
                    "Stock adjustment returned for changes.");
            }, token),
            cancellationToken);
    }

    public Task<ServiceResponse<StockAdjustmentDto>> CancelAsync(
        Guid stockAdjustmentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "stock-adjustment-cancel",
            idempotencyKey,
            stockAdjustmentId,
            token => transactionRunner.ExecuteSerializableAsync("stockAdjustment.cancel", async _ =>
            {
                var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
                var lineRepo = unitOfWork.Repository<StockAdjustmentLine>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

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

                // Cancel any still-pending approval alongside the document.
                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.StockAdjustment, adjustment.StockAdjustmentId, token);
                if (pendingApproval is not null)
                {
                    pendingApproval.Status = ApprovalDecisionStatus.Cancelled;
                    pendingApproval.DecidedById = userContext.GetCurrentUser().UserId;
                    pendingApproval.DecidedBy = userContext.GetCurrentUser().FullName;
                    pendingApproval.DecidedAt = DateTime.UtcNow;
                    pendingApproval.UpdatedAt = DateTime.UtcNow;
                    approvalRepo.Update(pendingApproval);
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
        var pagedAdjustments = await adjustmentRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<StockAdjustmentDto>
        {
            Records = pagedAdjustments.Records.Select(MapSummaryAdjustment).ToList(),
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

        var pattern = $"%{searchTerm.Trim()}%";
        var adjustmentRepo = unitOfWork.Repository<StockAdjustment>();
        var pagedAdjustments = await adjustmentRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            x => EF.Functions.ILike(x.AdjustmentNumber, pattern) ||
                 (x.Notes != null && EF.Functions.ILike(x.Notes, pattern)),
            cancellationToken);

        var response = new PaginationResult<StockAdjustmentDto>
        {
            Records = pagedAdjustments.Records.Select(MapSummaryAdjustment).ToList(),
            TotalRecords = pagedAdjustments.TotalRecords,
            PageSize = pagedAdjustments.PageSize,
            CurrentPage = pagedAdjustments.CurrentPage
        };

        return ServiceResponse<PaginationResult<StockAdjustmentDto>>.Success(response);
    }

    private async Task<string> GenerateUniqueAdjustmentNumberAsync(CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<StockAdjustment>();
        // Retry a few times in case the generator produces an existing number.
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

    private static async Task<ApprovalRequest?> GetPendingApprovalAsync(
        IBaseRepository<ApprovalRequest> approvalRepo,
        ApprovalDocumentType documentType,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var approvals = await approvalRepo.WhereAsync(
            x => x.DocumentType == documentType &&
                 x.DocumentId == documentId &&
                 x.Status == ApprovalDecisionStatus.Pending,
            cancellationToken);

        // Pick the newest pending record when approval history exists for the same document.
        return approvals
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefault();
    }

    private StockAdjustmentDto MapSummaryAdjustment(StockAdjustment adjustment)
    {
        var dto = mapper.Map<StockAdjustmentDto>(adjustment);
        dto.Lines = null;
        return dto;
    }

}
