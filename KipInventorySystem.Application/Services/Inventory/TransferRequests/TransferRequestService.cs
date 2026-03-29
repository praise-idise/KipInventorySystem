using Hangfire;
using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.TransferRequests;

public class TransferRequestService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IDocumentNumberGenerator documentNumberGenerator,
    IUserContext userContext,
    IMapper mapper,
    ILogger<TransferRequestService> logger) : ITransferRequestService
{
    public Task<ServiceResponse<TransferRequestDto>> CreateDraftAsync(
        CreateTransferRequestDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "transfer-request-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.createDraft", async _ =>
            {
                // Validate warehouses and products before creating the draft.
                var validation = await ValidateRequestReferencesAsync(request, token);
                if (validation is not null)
                {
                    return validation;
                }

                if (HasDuplicateProducts(request.Lines.Select(x => x.ProductId)))
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("Duplicate products are not allowed in transfer request lines.");
                }

                // Create the transfer header first so generated ids can be reused by the lines.
                var transfer = mapper.Map<TransferRequest>(request);
                transfer.TransferNumber = await GenerateUniqueTransferNumberAsync(token);
                transfer.Status = TransferRequestStatus.Draft;
                transfer.RequestedAt = DateTime.UtcNow;
                transfer.CreatedAt = DateTime.UtcNow;
                transfer.UpdatedAt = DateTime.UtcNow;

                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                await transferRepo.AddAsync(transfer, token);

                // Draft lines start with zero transferred quantity.
                var lines = mapper.Map<List<TransferRequestLine>>(request.Lines);
                foreach (var line in lines)
                {
                    line.TransferRequestId = transfer.TransferRequestId;
                    line.QuantityTransferred = 0;
                    line.CreatedAt = DateTime.UtcNow;
                    line.UpdatedAt = DateTime.UtcNow;
                }

                await lineRepo.AddRangeAsync(lines, token);
                transfer.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, status={Status}",
                    "CreateTransferRequestDraft",
                    userContext.GetCurrentUser().UserId,
                    transfer.TransferRequestId,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Created(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request draft created.");
            }, token),
            cancellationToken);
    }

    public async Task<ServiceResponse<TransferRequestDto>> SubmitAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var response = await idempotencyService.ExecuteAsync(
            "transfer-request-submit",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.submit", async _ =>
            {
                // Submission moves a draft into the approval workflow.
                var currentUser = userContext.GetCurrentUser();
                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("Transfer request has no lines.");
                }

                if (transfer.Status == TransferRequestStatus.PendingApproval)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request is already awaiting approval.");
                }

                if (transfer.Status is TransferRequestStatus.Approved or TransferRequestStatus.InTransit or TransferRequestStatus.Completed)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request is already approved.");
                }

                if (transfer.Status is not TransferRequestStatus.Draft and not TransferRequestStatus.ChangesRequested)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only draft or returned transfer requests can be submitted for approval.");
                }

                // Only one pending approval request should exist per transfer.
                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.TransferRequest, transfer.TransferRequestId, token);
                if (pendingApproval is not null)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("This transfer request already has a pending approval request.");
                }

                transfer.Status = TransferRequestStatus.PendingApproval;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);

                await approvalRepo.AddAsync(new ApprovalRequest
                {
                    DocumentType = ApprovalDocumentType.TransferRequest,
                    DocumentId = transfer.TransferRequestId,
                    Status = ApprovalDecisionStatus.Pending,
                    RequestedById = currentUser.UserId,
                    RequestedBy = currentUser.FullName,
                    RequestedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, token);

                transfer.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, newStatus={NewStatus}",
                    "SubmitTransferRequestForApproval",
                    currentUser.UserId,
                    transfer.TransferRequestId,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request submitted for approval.");
            }, token),
            cancellationToken);

        return response;
    }

    public async Task<ServiceResponse<TransferRequestDto>> ApproveAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync(
            "transfer-request-approve",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.approve", async _ =>
            {
                // Approval reserves stock in the source warehouse.
                var currentUser = userContext.GetCurrentUser();
                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("Transfer request has no lines.");
                }

                if (transfer.Status == TransferRequestStatus.Approved)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request already approved.");
                }

                if (transfer.Status != TransferRequestStatus.PendingApproval)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only transfer requests awaiting approval can be approved.");
                }

                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.TransferRequest, transfer.TransferRequestId, token);
                if (pendingApproval is null)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("No pending approval request was found for this transfer request.");
                }

                // Check availability before making any reservation changes.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.SourceWarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null || inventory.AvailableQuantity < line.QuantityRequested)
                    {
                        return ServiceResponse<TransferRequestDto>.Conflict(
                            $"Insufficient available stock for product '{line.ProductId}' in source warehouse.");
                    }
                }

                // Reserve each approved quantity in the source warehouse.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.SourceWarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        continue;
                    }

                    inventory.ReservedQuantity += line.QuantityRequested;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProducts.Add(line.ProductId);
                }

                transfer.Status = TransferRequestStatus.Approved;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);
                transfer.Lines = lines;
                affectedWarehouse = transfer.SourceWarehouseId;

                pendingApproval.Status = ApprovalDecisionStatus.Approved;
                pendingApproval.DecidedById = currentUser.UserId;
                pendingApproval.DecidedBy = currentUser.FullName;
                pendingApproval.DecidedAt = DateTime.UtcNow;
                pendingApproval.UpdatedAt = DateTime.UtcNow;
                approvalRepo.Update(pendingApproval);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, newStatus={NewStatus}",
                    "ApproveTransferRequest",
                    currentUser.UserId,
                    transfer.TransferRequestId,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request approved.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public Task<ServiceResponse<TransferRequestDto>> ReturnForChangesAsync(
        Guid transferRequestId,
        ApprovalDecisionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "transfer-request-return",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.returnForChanges", async _ =>
            {
                // A return decision must include a reason for the requester.
                if (string.IsNullOrWhiteSpace(request.Comment))
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("A comment is required when returning a transfer request for changes.");
                }

                var currentUser = userContext.GetCurrentUser();
                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                transfer.Lines = lines;

                if (transfer.Status != TransferRequestStatus.PendingApproval)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only transfer requests awaiting approval can be returned for changes.");
                }

                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.TransferRequest, transfer.TransferRequestId, token);
                if (pendingApproval is null)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("No pending approval request was found for this transfer request.");
                }

                // Move the transfer back to an editable state and store the review comment.
                transfer.Status = TransferRequestStatus.ChangesRequested;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);

                pendingApproval.Status = ApprovalDecisionStatus.ChangesRequested;
                pendingApproval.Comment = request.Comment.Trim();
                pendingApproval.DecidedById = currentUser.UserId;
                pendingApproval.DecidedBy = currentUser.FullName;
                pendingApproval.DecidedAt = DateTime.UtcNow;
                pendingApproval.UpdatedAt = DateTime.UtcNow;
                approvalRepo.Update(pendingApproval);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, newStatus={NewStatus}",
                    "ReturnTransferRequestForChanges",
                    currentUser.UserId,
                    transfer.TransferRequestId,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request returned for changes.");
            }, token),
            cancellationToken);
    }

    public async Task<ServiceResponse<TransferRequestDto>> DispatchAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync(
            "transfer-request-dispatch",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.dispatch", async _ =>
            {
                // Dispatch consumes the reserved stock and records transfer-out movements.
                var currentUser = userContext.GetCurrentUser();
                var movementCreatorId = currentUser.UserId;
                var movementCreator = currentUser.FullName;

                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("Transfer request has no lines.");
                }

                if (transfer.Status == TransferRequestStatus.InTransit || transfer.Status == TransferRequestStatus.Completed)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request already dispatched.");
                }

                if (transfer.Status != TransferRequestStatus.Approved)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only approved transfer requests can be dispatched.");
                }

                // Dispatch can only use stock that is still on hand and reserved.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.SourceWarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null || inventory.QuantityOnHand < line.QuantityRequested || inventory.ReservedQuantity < line.QuantityRequested)
                    {
                        return ServiceResponse<TransferRequestDto>.Conflict(
                            $"Insufficient reserved stock for product '{line.ProductId}' in source warehouse.");
                    }
                }

                var movements = new List<StockMovement>();
                // Apply the outbound inventory change per line.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.SourceWarehouseId && x.ProductId == line.ProductId,
                        token);
                    if (inventory is null)
                    {
                        continue;
                    }

                    var (unitCost, totalCost) = InventoryCosting.ApplyOutbound(inventory, line.QuantityRequested);
                    inventory.ReservedQuantity -= line.QuantityRequested;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProducts.Add(line.ProductId);

                    line.QuantityTransferred = line.QuantityRequested;
                    line.UpdatedAt = DateTime.UtcNow;
                    lineRepo.Update(line);

                    movements.Add(new StockMovement
                    {
                        ProductId = line.ProductId,
                        WarehouseId = transfer.SourceWarehouseId,
                        MovementType = StockMovementType.TransferOut,
                        Quantity = line.QuantityRequested,
                        UnitCost = unitCost,
                        TotalCost = totalCost,
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.TransferRequest,
                        ReferenceId = transfer.TransferRequestId,
                        Creator = movementCreator,
                        CreatorId = movementCreatorId,
                        Notes = transfer.Notes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await movementRepo.AddRangeAsync(movements, token);

                transfer.Status = TransferRequestStatus.InTransit;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);
                transfer.Lines = lines;
                affectedWarehouse = transfer.SourceWarehouseId;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, oldStatus={OldStatus}, newStatus={NewStatus}",
                    "DispatchTransferRequest",
                    userContext.GetCurrentUser().UserId,
                    transfer.TransferRequestId,
                    TransferRequestStatus.Approved,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request dispatched.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public async Task<ServiceResponse<TransferRequestDto>> CompleteAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync(
            "transfer-request-complete",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.complete", async _ =>
            {
                // Completion receives the stock into the destination warehouse.
                var currentUser = userContext.GetCurrentUser();
                var movementCreatorId = currentUser.UserId;
                var movementCreator = currentUser.FullName;

                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("Transfer request has no lines.");
                }

                if (transfer.Status == TransferRequestStatus.Completed)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request already completed.");
                }

                if (transfer.Status != TransferRequestStatus.InTransit)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only in-transit transfer requests can be completed.");
                }

                // Reuse the latest transfer-out cost so inbound valuation stays aligned.
                var transferOutMovements = await movementRepo.WhereAsync(
                    x => x.ReferenceType == StockMovementReferenceType.TransferRequest &&
                         x.ReferenceId == transfer.TransferRequestId &&
                         x.MovementType == StockMovementType.TransferOut,
                    token);

                var transferOutUnitCostByProduct = transferOutMovements
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(x => x.Key, x => x.OrderByDescending(m => m.OccurredAt).First().UnitCost);

                var movements = new List<StockMovement>();
                // Receive each line into destination inventory and create transfer-in movements.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.DestinationWarehouseId && x.ProductId == line.ProductId,
                        token);
                    var qty = line.QuantityTransferred > 0 ? line.QuantityTransferred : line.QuantityRequested;
                    var inboundUnitCost = transferOutUnitCostByProduct.TryGetValue(line.ProductId, out var transferOutUnitCost)
                        ? transferOutUnitCost
                        : inventory?.AverageUnitCost ?? 0m;

                    if (!transferOutUnitCostByProduct.ContainsKey(line.ProductId))
                    {
                        // Fall back to current average cost if the transfer-out valuation is missing.
                        logger.LogWarning(
                            "Transfer-in valuation fallback used because transfer-out movement cost was not found. TransferRequestId={TransferRequestId}, ProductId={ProductId}, UnitCost={UnitCost}",
                            transfer.TransferRequestId,
                            line.ProductId,
                            inboundUnitCost);
                    }

                    if (inventory is null)
                    {
                        var roundedUnitCost = InventoryCosting.Round(inboundUnitCost);
                        var totalCost = InventoryCosting.Round(roundedUnitCost * qty);
                        inventory = new WarehouseInventory
                        {
                            WarehouseId = transfer.DestinationWarehouseId,
                            ProductId = line.ProductId,
                            QuantityOnHand = qty,
                            ReservedQuantity = 0,
                            AverageUnitCost = roundedUnitCost,
                            InventoryValue = totalCost,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await inventoryRepo.AddAsync(inventory, token);

                        movements.Add(new StockMovement
                        {
                            ProductId = line.ProductId,
                            WarehouseId = transfer.DestinationWarehouseId,
                            MovementType = StockMovementType.TransferIn,
                            Quantity = qty,
                            UnitCost = roundedUnitCost,
                            TotalCost = totalCost,
                            OccurredAt = DateTime.UtcNow,
                            ReferenceType = StockMovementReferenceType.TransferRequest,
                            ReferenceId = transfer.TransferRequestId,
                            Creator = movementCreator,
                            CreatorId = movementCreatorId,
                            Notes = transfer.Notes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        var (unitCost, totalCost) = InventoryCosting.ApplyInbound(inventory, qty, inboundUnitCost);
                        inventory.UpdatedAt = DateTime.UtcNow;
                        inventoryRepo.Update(inventory);

                        movements.Add(new StockMovement
                        {
                            ProductId = line.ProductId,
                            WarehouseId = transfer.DestinationWarehouseId,
                            MovementType = StockMovementType.TransferIn,
                            Quantity = qty,
                            UnitCost = unitCost,
                            TotalCost = totalCost,
                            OccurredAt = DateTime.UtcNow,
                            ReferenceType = StockMovementReferenceType.TransferRequest,
                            ReferenceId = transfer.TransferRequestId,
                            Creator = movementCreator,
                            CreatorId = movementCreatorId,
                            Notes = transfer.Notes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    affectedProducts.Add(line.ProductId);
                }

                await movementRepo.AddRangeAsync(movements, token);

                transfer.Status = TransferRequestStatus.Completed;
                transfer.CompletedAt = DateTime.UtcNow;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);
                transfer.Lines = lines;
                affectedWarehouse = transfer.DestinationWarehouseId;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, oldStatus={OldStatus}, newStatus={NewStatus}",
                    "CompleteTransferRequest",
                    userContext.GetCurrentUser().UserId,
                    transfer.TransferRequestId,
                    TransferRequestStatus.InTransit,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request completed.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public async Task<ServiceResponse<TransferRequestDto>> CancelAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync(
            "transfer-request-cancel",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.cancel", async _ =>
            {
                // Cancellation may need to release previously reserved stock.
                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var approvalRepo = unitOfWork.Repository<ApprovalRequest>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                transfer.Lines = lines;
                if (transfer.Status == TransferRequestStatus.Cancelled)
                {
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request already cancelled.");
                }

                if (transfer.Status is TransferRequestStatus.InTransit or TransferRequestStatus.Completed)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("In-transit or completed transfer requests cannot be cancelled.");
                }

                if (transfer.Status == TransferRequestStatus.Approved)
                {
                    // Release reservations only when approval had already reserved stock.
                    foreach (var line in lines)
                    {
                        var inventory = await inventoryRepo.FindAsync(
                            x => x.WarehouseId == transfer.SourceWarehouseId && x.ProductId == line.ProductId,
                            token);

                        if (inventory is null)
                        {
                            continue;
                        }

                        inventory.ReservedQuantity = Math.Max(0, inventory.ReservedQuantity - line.QuantityRequested);
                        inventory.UpdatedAt = DateTime.UtcNow;
                        inventoryRepo.Update(inventory);
                        affectedProducts.Add(line.ProductId);
                    }

                    affectedWarehouse = transfer.SourceWarehouseId;
                }

                // Close any pending approval alongside the document cancellation.
                var pendingApproval = await GetPendingApprovalAsync(approvalRepo, ApprovalDocumentType.TransferRequest, transfer.TransferRequestId, token);
                if (pendingApproval is not null)
                {
                    pendingApproval.Status = ApprovalDecisionStatus.Cancelled;
                    pendingApproval.DecidedById = userContext.GetCurrentUser().UserId;
                    pendingApproval.DecidedBy = userContext.GetCurrentUser().FullName;
                    pendingApproval.DecidedAt = DateTime.UtcNow;
                    pendingApproval.UpdatedAt = DateTime.UtcNow;
                    approvalRepo.Update(pendingApproval);
                }

                transfer.Status = TransferRequestStatus.Cancelled;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, newStatus={NewStatus}",
                    "CancelTransferRequest",
                    userContext.GetCurrentUser().UserId,
                    transfer.TransferRequestId,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request cancelled.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public async Task<ServiceResponse<TransferRequestDto>> GetByIdAsync(Guid transferRequestId, CancellationToken cancellationToken = default)
    {
        var transferRepo = unitOfWork.Repository<TransferRequest>();
        var lineRepo = unitOfWork.Repository<TransferRequestLine>();
        var transfer = await transferRepo.GetByIdAsync(transferRequestId, cancellationToken);
        if (transfer is null)
        {
            return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
        }

        // Detail view includes the transfer lines.
        transfer.Lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, cancellationToken);
        return ServiceResponse<TransferRequestDto>.Success(mapper.Map<TransferRequestDto>(transfer));
    }

    public async Task<ServiceResponse<PaginationResult<TransferRequestDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var transferRepo = unitOfWork.Repository<TransferRequest>();
        var pagedTransfers = await transferRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            cancellationToken: cancellationToken);

        // List view returns transfer headers only; lines stay on the detail endpoint.
        var response = new PaginationResult<TransferRequestDto>
        {
            Records = pagedTransfers.Records.Select(x => mapper.Map<TransferRequestDto>(x)).ToList(),
            TotalRecords = pagedTransfers.TotalRecords,
            PageSize = pagedTransfers.PageSize,
            CurrentPage = pagedTransfers.CurrentPage
        };

        return ServiceResponse<PaginationResult<TransferRequestDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<TransferRequestDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var transferRepo = unitOfWork.Repository<TransferRequest>();
        var pagedTransfers = await transferRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            x => x.TransferNumber.ToLower().Contains(term) ||
                 (x.Notes != null && x.Notes.ToLower().Contains(term)),
            cancellationToken);

        // Search results also return transfer headers only for lighter queries.
        var response = new PaginationResult<TransferRequestDto>
        {
            Records = pagedTransfers.Records.Select(x => mapper.Map<TransferRequestDto>(x)).ToList(),
            TotalRecords = pagedTransfers.TotalRecords,
            PageSize = pagedTransfers.PageSize,
            CurrentPage = pagedTransfers.CurrentPage
        };

        return ServiceResponse<PaginationResult<TransferRequestDto>>.Success(response);
    }

    private async Task<ServiceResponse<TransferRequestDto>?> ValidateRequestReferencesAsync(
        CreateTransferRequestDraftRequest request,
        CancellationToken cancellationToken)
    {
        // Transfers must move stock between two different warehouses.
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            return ServiceResponse<TransferRequestDto>.BadRequest("Source and destination warehouses must be different.");
        }

        // Both warehouse ids must exist before a draft can be saved.
        var warehouseRepo = unitOfWork.Repository<Warehouse>();
        var source = await warehouseRepo.GetByIdAsync(request.SourceWarehouseId, cancellationToken);
        if (source is null)
        {
            return ServiceResponse<TransferRequestDto>.BadRequest("Source warehouse was not found.");
        }

        var destination = await warehouseRepo.GetByIdAsync(request.DestinationWarehouseId, cancellationToken);
        if (destination is null)
        {
            return ServiceResponse<TransferRequestDto>.BadRequest("Destination warehouse was not found.");
        }

        // Validate each distinct product once.
        var productRepo = unitOfWork.Repository<Product>();
        foreach (var productId in request.Lines.Select(x => x.ProductId).Distinct())
        {
            var product = await productRepo.GetByIdAsync(productId, cancellationToken);
            if (product is null)
            {
                return ServiceResponse<TransferRequestDto>.BadRequest($"Product '{productId}' was not found.");
            }
        }

        return null;
    }

    private async Task<string> GenerateUniqueTransferNumberAsync(CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<TransferRequest>();
        // Retry a few generated numbers before failing the transaction.
        for (var i = 0; i < 5; i++)
        {
            var number = documentNumberGenerator.GenerateTransferNumber();
            var exists = await repo.ExistsAsync(x => x.TransferNumber == number, cancellationToken);
            if (!exists)
            {
                return number;
            }
        }

        throw new InvalidOperationException("Unable to generate unique transfer number.");
    }

    private static bool HasDuplicateProducts(IEnumerable<Guid> productIds)
        => productIds.GroupBy(x => x).Any(x => x.Count() > 1);

    private static void EnqueueLowStockChecks(bool succeeded, Guid? warehouseId, IEnumerable<Guid> productIds)
    {
        if (!succeeded || warehouseId is null)
        {
            return;
        }

        // Recheck only products touched by the transfer workflow.
        foreach (var productId in productIds.Distinct())
        {
            BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                "default",
                jobs => jobs.EvaluateLowStockAsync(warehouseId.Value, productId, default));
        }
    }

    private static async Task<ApprovalRequest?> GetPendingApprovalAsync(
        IBaseRepository<ApprovalRequest> approvalRepo,
        ApprovalDocumentType documentType,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        // Use the latest pending approval when multiple decisions exist historically.
        var approvals = await approvalRepo.WhereAsync(
            x => x.DocumentType == documentType &&
                 x.DocumentId == documentId &&
                 x.Status == ApprovalDecisionStatus.Pending,
            cancellationToken);

        return approvals
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefault();
    }

}
