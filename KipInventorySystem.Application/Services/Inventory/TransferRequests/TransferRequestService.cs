using Hangfire;
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
        return idempotencyService.ExecuteAsync<CreateTransferRequestDraftRequest, TransferRequestDto>(
            "transfer-request-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.createDraft", async _ =>
            {
                var validation = await ValidateRequestReferencesAsync(request, token);
                if (validation is not null)
                {
                    return validation;
                }

                if (HasDuplicateProducts(request.Lines.Select(x => x.ProductId)))
                {
                    return ServiceResponse<TransferRequestDto>.BadRequest("Duplicate products are not allowed in transfer request lines.");
                }

                var transfer = mapper.Map<TransferRequest>(request);
                transfer.TransferNumber = await GenerateUniqueTransferNumberAsync(token);
                transfer.Status = TransferRequestStatus.Draft;
                transfer.RequestedAt = DateTime.UtcNow;
                transfer.CreatedAt = DateTime.UtcNow;
                transfer.UpdatedAt = DateTime.UtcNow;

                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                await transferRepo.AddAsync(transfer, token);

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
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync<Guid, TransferRequestDto>(
            "transfer-request-submit",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.submit", async _ =>
            {
                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();

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

                if (transfer.Status == TransferRequestStatus.Submitted ||
                    transfer.Status == TransferRequestStatus.InTransit ||
                    transfer.Status == TransferRequestStatus.Completed)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request already submitted.");
                }

                if (transfer.Status != TransferRequestStatus.Draft)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only draft transfer requests can be submitted.");
                }

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

                transfer.Status = TransferRequestStatus.Submitted;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);
                transfer.Lines = lines;
                affectedWarehouse = transfer.SourceWarehouseId;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=TransferRequest, entityId={EntityId}, oldStatus={OldStatus}, newStatus={NewStatus}",
                    "SubmitTransferRequest",
                    userContext.GetCurrentUser().UserId,
                    transfer.TransferRequestId,
                    TransferRequestStatus.Draft,
                    transfer.Status);

                return ServiceResponse<TransferRequestDto>.Success(
                    mapper.Map<TransferRequestDto>(transfer),
                    "Transfer request submitted.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public async Task<ServiceResponse<TransferRequestDto>> DispatchAsync(
        Guid transferRequestId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync<Guid, TransferRequestDto>(
            "transfer-request-dispatch",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.dispatch", async _ =>
            {
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

                if (transfer.Status != TransferRequestStatus.Submitted)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("Only submitted transfer requests can be dispatched.");
                }

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
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.SourceWarehouseId && x.ProductId == line.ProductId,
                        token);
                    if (inventory is null)
                    {
                        continue;
                    }

                    inventory.QuantityOnHand -= line.QuantityRequested;
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
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.TransferRequest,
                        ReferenceId = transfer.TransferRequestId,
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
                    TransferRequestStatus.Submitted,
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

        var response = await idempotencyService.ExecuteAsync<Guid, TransferRequestDto>(
            "transfer-request-complete",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.complete", async _ =>
            {
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

                var movements = new List<StockMovement>();
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == transfer.DestinationWarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        inventory = new WarehouseInventory
                        {
                            WarehouseId = transfer.DestinationWarehouseId,
                            ProductId = line.ProductId,
                            QuantityOnHand = 0,
                            ReservedQuantity = 0,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await inventoryRepo.AddAsync(inventory, token);
                    }

                    var qty = line.QuantityTransferred > 0 ? line.QuantityTransferred : line.QuantityRequested;
                    inventory.QuantityOnHand += qty;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProducts.Add(line.ProductId);

                    movements.Add(new StockMovement
                    {
                        ProductId = line.ProductId,
                        WarehouseId = transfer.DestinationWarehouseId,
                        MovementType = StockMovementType.TransferIn,
                        Quantity = qty,
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.TransferRequest,
                        ReferenceId = transfer.TransferRequestId,
                        Notes = transfer.Notes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
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

        var response = await idempotencyService.ExecuteAsync<Guid, TransferRequestDto>(
            "transfer-request-cancel",
            idempotencyKey,
            transferRequestId,
            token => transactionRunner.ExecuteSerializableAsync("transferRequest.cancel", async _ =>
            {
                var transferRepo = unitOfWork.Repository<TransferRequest>();
                var lineRepo = unitOfWork.Repository<TransferRequestLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();

                var transfer = await transferRepo.GetByIdAsync(transferRequestId, token);
                if (transfer is null)
                {
                    return ServiceResponse<TransferRequestDto>.NotFound("Transfer request was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, token);
                if (transfer.Status == TransferRequestStatus.Cancelled)
                {
                    transfer.Lines = lines;
                    return ServiceResponse<TransferRequestDto>.Success(
                        mapper.Map<TransferRequestDto>(transfer),
                        "Transfer request already cancelled.");
                }

                if (transfer.Status is TransferRequestStatus.InTransit or TransferRequestStatus.Completed)
                {
                    return ServiceResponse<TransferRequestDto>.Conflict("In-transit or completed transfer requests cannot be cancelled.");
                }

                if (transfer.Status == TransferRequestStatus.Submitted)
                {
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

                transfer.Status = TransferRequestStatus.Cancelled;
                transfer.UpdatedAt = DateTime.UtcNow;
                transferRepo.Update(transfer);
                transfer.Lines = lines;

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

        transfer.Lines = await lineRepo.WhereAsync(x => x.TransferRequestId == transferRequestId, cancellationToken);
        return ServiceResponse<TransferRequestDto>.Success(mapper.Map<TransferRequestDto>(transfer));
    }

    public async Task<ServiceResponse<PaginationResult<TransferRequestDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var transferRepo = unitOfWork.Repository<TransferRequest>();
        var lineRepo = unitOfWork.Repository<TransferRequestLine>();
        var pagedTransfers = await transferRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            cancellationToken: cancellationToken);

        var transfers = pagedTransfers.Records.ToList();
        if (transfers.Count > 0)
        {
            var ids = transfers.Select(x => x.TransferRequestId).ToHashSet();
            var lines = await lineRepo.WhereAsync(x => ids.Contains(x.TransferRequestId), cancellationToken);
            var grouped = lines.GroupBy(x => x.TransferRequestId).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var transfer in transfers)
            {
                if (grouped.TryGetValue(transfer.TransferRequestId, out var transferLines))
                {
                    transfer.Lines = transferLines;
                }
            }
        }

        var response = new PaginationResult<TransferRequestDto>
        {
            Records = transfers.Select(x => mapper.Map<TransferRequestDto>(x)).ToList(),
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
        var lineRepo = unitOfWork.Repository<TransferRequestLine>();
        var pagedTransfers = await transferRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.RequestedAt),
            x => x.TransferNumber.ToLower().Contains(term) ||
                 (x.Notes != null && x.Notes.ToLower().Contains(term)),
            cancellationToken);

        var transfers = pagedTransfers.Records.ToList();
        if (transfers.Count > 0)
        {
            var ids = transfers.Select(x => x.TransferRequestId).ToHashSet();
            var lines = await lineRepo.WhereAsync(x => ids.Contains(x.TransferRequestId), cancellationToken);
            var grouped = lines.GroupBy(x => x.TransferRequestId).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var transfer in transfers)
            {
                if (grouped.TryGetValue(transfer.TransferRequestId, out var transferLines))
                {
                    transfer.Lines = transferLines;
                }
            }
        }

        var response = new PaginationResult<TransferRequestDto>
        {
            Records = transfers.Select(x => mapper.Map<TransferRequestDto>(x)).ToList(),
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
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            return ServiceResponse<TransferRequestDto>.BadRequest("Source and destination warehouses must be different.");
        }

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

    private void EnqueueLowStockChecks(bool succeeded, Guid? warehouseId, IEnumerable<Guid> productIds)
    {
        if (!succeeded || warehouseId is null)
        {
            return;
        }

        foreach (var productId in productIds.Distinct())
        {
            BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                "default",
                jobs => jobs.EvaluateLowStockAsync(warehouseId.Value, productId, default));
        }
    }

}
