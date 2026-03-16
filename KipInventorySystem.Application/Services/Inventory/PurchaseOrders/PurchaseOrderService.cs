using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.PurchaseOrders;

public class PurchaseOrderService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IDocumentNumberGenerator documentNumberGenerator,
    IUserContext userContext,
    IMapper mapper,
    ILogger<PurchaseOrderService> logger) : IPurchaseOrderService
{
    public Task<ServiceResponse<PurchaseOrderDTO>> CreateDraftAsync(
        CreatePurchaseOrderDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "purchase-order-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("purchaseOrder.createDraft", async _ =>
            {
                var validation = await ValidateReferencesAsync(
                    request.SupplierId,
                    request.WarehouseId,
                    [.. request.Lines.Select(x => x.ProductId)],
                    token);

                if (validation is not null)
                {
                    return validation;
                }

                if (HasDuplicateProducts(request.Lines.Select(x => x.ProductId)))
                {
                    return ServiceResponse<PurchaseOrderDTO>.BadRequest("Duplicate products are not allowed in a purchase order.");
                }

                var po = mapper.Map<PurchaseOrder>(request);
                po.PurchaseOrderNumber = await GenerateUniquePurchaseOrderNumberAsync(token);
                po.Status = PurchaseOrderStatus.Draft;
                po.OrderedAt = DateTime.UtcNow;
                po.CreatedAt = DateTime.UtcNow;
                po.UpdatedAt = DateTime.UtcNow;

                var poRepo = unitOfWork.Repository<PurchaseOrder>();
                var lines = mapper.Map<List<PurchaseOrderLine>>(request.Lines);
                foreach (var line in lines)
                {
                    line.PurchaseOrderId = po.PurchaseOrderId;
                    line.QuantityReceived = 0;
                    line.CreatedAt = DateTime.UtcNow;
                    line.UpdatedAt = DateTime.UtcNow;
                }

                po.Lines = lines;
                await poRepo.AddAsync(po, token);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=PurchaseOrder, entityId={EntityId}, status={Status}",
                    "CreatePurchaseOrderDraft",
                    userContext.GetCurrentUser().UserId,
                    po.PurchaseOrderId,
                    po.Status);

                return ServiceResponse<PurchaseOrderDTO>.Created(
                    mapper.Map<PurchaseOrderDTO>(po),
                    "Purchase order draft created.");
            }, token),
            cancellationToken);
    }

    public Task<ServiceResponse<PurchaseOrderDTO>> UpdateDraftAsync(
        Guid purchaseOrderId,
        UpdatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        return transactionRunner.ExecuteSerializableAsync("purchaseOrder.updateDraft", async token =>
        {
            var poRepo = unitOfWork.Repository<PurchaseOrder>();
            var lineRepo = unitOfWork.Repository<PurchaseOrderLine>();
            var po = await poRepo.GetByIdAsync(purchaseOrderId, token);
            if (po is null)
            {
                return ServiceResponse<PurchaseOrderDTO>.NotFound("Purchase order was not found.");
            }

            if (po.Status != PurchaseOrderStatus.Draft)
            {
                return ServiceResponse<PurchaseOrderDTO>.Conflict("Only draft purchase orders can be updated.");
            }

            var effectiveSupplierId = request.SupplierId ?? po.SupplierId;
            var effectiveWarehouseId = request.WarehouseId ?? po.WarehouseId;

            if (request.Lines is not null && HasDuplicateProducts(request.Lines.Select(x => x.ProductId)))
            {
                return ServiceResponse<PurchaseOrderDTO>.BadRequest("Duplicate products are not allowed in a purchase order.");
            }

            if (request.SupplierId.HasValue || request.WarehouseId.HasValue || request.Lines is not null)
            {
                List<Guid> productIds;

                if (request.Lines is not null)
                {
                    productIds = [.. request.Lines.Select(x => x.ProductId).Distinct()];
                }
                else
                {
                    productIds = [.. (await lineRepo.WhereAsync(x => x.PurchaseOrderId == purchaseOrderId, token))
                        .Select(x => x.ProductId)
                        .Distinct()];
                }

                var validation = await ValidateReferencesAsync(
                    effectiveSupplierId,
                    effectiveWarehouseId,
                    productIds,
                    token);

                if (validation is not null)
                {
                    return validation;
                }
            }

            if (request.SupplierId.HasValue)
            {
                po.SupplierId = request.SupplierId.Value;
            }

            if (request.WarehouseId.HasValue)
            {
                po.WarehouseId = request.WarehouseId.Value;
            }

            if (request.ExpectedArrivalDate.HasValue)
            {
                po.ExpectedArrivalDate = request.ExpectedArrivalDate.Value;
            }

            if (request.Notes is not null)
            {
                po.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            }

            po.UpdatedAt = DateTime.UtcNow;
            poRepo.Update(po);

            if (request.Lines is not null)
            {
                var existingLines = await lineRepo.WhereAsync(x => x.PurchaseOrderId == purchaseOrderId, token);
                if (existingLines.Count > 0)
                {
                    lineRepo.RemoveRange(existingLines);
                }

                var newLines = mapper.Map<List<PurchaseOrderLine>>(request.Lines);
                foreach (var line in newLines)
                {
                    line.PurchaseOrderId = purchaseOrderId;
                    line.QuantityReceived = 0;
                    line.CreatedAt = DateTime.UtcNow;
                    line.UpdatedAt = DateTime.UtcNow;
                }

                await lineRepo.AddRangeAsync(newLines, token);
                po.Lines = newLines;
            }
            else
            {
                po.Lines = await lineRepo.WhereAsync(x => x.PurchaseOrderId == purchaseOrderId, token);
            }

            logger.LogInformation(
                "Inventory audit: operation={Operation}, actor={Actor}, entity=PurchaseOrder, entityId={EntityId}",
                "UpdatePurchaseOrderDraft",
                userContext.GetCurrentUser().UserId,
                po.PurchaseOrderId);

            return ServiceResponse<PurchaseOrderDTO>.Success(
                mapper.Map<PurchaseOrderDTO>(po),
                "Purchase order draft updated.");
        }, cancellationToken);
    }

    public Task<ServiceResponse<PurchaseOrderDTO>> SubmitAsync(
        Guid purchaseOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "purchase-order-submit",
            idempotencyKey,
            purchaseOrderId,
            token => transactionRunner.ExecuteSerializableAsync("purchaseOrder.submit", async _ =>
            {
                var poRepo = unitOfWork.Repository<PurchaseOrder>();
                var lineRepo = unitOfWork.Repository<PurchaseOrderLine>();

                var po = await poRepo.GetByIdAsync(purchaseOrderId, token);
                if (po is null)
                {
                    return ServiceResponse<PurchaseOrderDTO>.NotFound("Purchase order was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.PurchaseOrderId == purchaseOrderId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<PurchaseOrderDTO>.BadRequest("Cannot submit a purchase order without lines.");
                }

                if (lines.Any(x => x.QuantityOrdered <= 0))
                {
                    return ServiceResponse<PurchaseOrderDTO>.BadRequest("Purchase order lines must have quantity greater than zero.");
                }

                if (po.Status == PurchaseOrderStatus.Submitted ||
                    po.Status == PurchaseOrderStatus.PartiallyReceived ||
                    po.Status == PurchaseOrderStatus.Received)
                {
                    po.Lines = lines;
                    return ServiceResponse<PurchaseOrderDTO>.Success(
                        mapper.Map<PurchaseOrderDTO>(po),
                        "Purchase order already submitted.");
                }

                if (po.Status != PurchaseOrderStatus.Draft)
                {
                    return ServiceResponse<PurchaseOrderDTO>.Conflict("Only draft purchase orders can be submitted.");
                }

                po.Status = PurchaseOrderStatus.Submitted;
                po.OrderedAt = DateTime.UtcNow;
                po.UpdatedAt = DateTime.UtcNow;
                poRepo.Update(po);
                po.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=PurchaseOrder, entityId={EntityId}, oldStatus={OldStatus}, newStatus={NewStatus}",
                    "SubmitPurchaseOrder",
                    userContext.GetCurrentUser().UserId,
                    po.PurchaseOrderId,
                    PurchaseOrderStatus.Draft,
                    po.Status);

                return ServiceResponse<PurchaseOrderDTO>.Success(
                    mapper.Map<PurchaseOrderDTO>(po),
                    "Purchase order submitted.");
            }, token),
            cancellationToken);
    }

    public async Task<ServiceResponse<PurchaseOrderDTO>> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var poRepo = unitOfWork.Repository<PurchaseOrder>();
        var po = await poRepo.GetByIdAsync(
            purchaseOrderId,
            query => query
                .Include(x => x.Warehouse)
                .Include(x => x.Lines)
                .ThenInclude(x => x.Product),
            cancellationToken);
        if (po is null)
        {
            return ServiceResponse<PurchaseOrderDTO>.NotFound("Purchase order was not found.");
        }

        return ServiceResponse<PurchaseOrderDTO>.Success(mapper.Map<PurchaseOrderDTO>(po));
    }

    public async Task<ServiceResponse<PaginationResult<PurchaseOrderDTO>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var poRepo = unitOfWork.Repository<PurchaseOrder>();
        var pagedOrders = await poRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.OrderedAt),
            cancellationToken: cancellationToken,
            include: query => query.Include(x => x.Warehouse));

        var response = new PaginationResult<PurchaseOrderDTO>
        {
            Records = [.. pagedOrders.Records.Select(MapPurchaseOrderSummary)],
            TotalRecords = pagedOrders.TotalRecords,
            PageSize = pagedOrders.PageSize,
            CurrentPage = pagedOrders.CurrentPage
        };

        return ServiceResponse<PaginationResult<PurchaseOrderDTO>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<PurchaseOrderDTO>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var poRepo = unitOfWork.Repository<PurchaseOrder>();
        var pagedOrders = await poRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.OrderedAt),
            x => x.PurchaseOrderNumber.ToLower().Contains(term) ||
                 (x.Notes != null && x.Notes.ToLower().Contains(term)),
            cancellationToken,
            query => query.Include(x => x.Warehouse));

        var response = new PaginationResult<PurchaseOrderDTO>
        {
            Records = pagedOrders.Records.Select(MapPurchaseOrderSummary).ToList(),
            TotalRecords = pagedOrders.TotalRecords,
            PageSize = pagedOrders.PageSize,
            CurrentPage = pagedOrders.CurrentPage
        };

        return ServiceResponse<PaginationResult<PurchaseOrderDTO>>.Success(response);
    }

    private async Task<ServiceResponse<PurchaseOrderDTO>?> ValidateReferencesAsync(
        Guid supplierId,
        Guid warehouseId,
        List<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var supplier = await unitOfWork.Repository<Supplier>().GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return ServiceResponse<PurchaseOrderDTO>.BadRequest("Supplier was not found.");
        }

        var warehouse = await unitOfWork.Repository<Warehouse>().GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return ServiceResponse<PurchaseOrderDTO>.BadRequest("Warehouse was not found.");
        }

        var productRepo = unitOfWork.Repository<Product>();
        var productSupplierRepo = unitOfWork.Repository<ProductSupplier>();
        foreach (var productId in productIds.Distinct())
        {
            var product = await productRepo.GetByIdAsync(productId, cancellationToken);
            if (product is null)
            {
                return ServiceResponse<PurchaseOrderDTO>.BadRequest($"Product '{productId}' was not found.");
            }

            var productSupplier = await productSupplierRepo.FindAsync(
                x => x.ProductId == productId && x.SupplierId == supplierId,
                cancellationToken);

            if (productSupplier is null)
            {
                return ServiceResponse<PurchaseOrderDTO>.BadRequest(
                    $"Product '{product.Name}' is not linked to the selected supplier.");
            }
        }

        return null;
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

        throw new InvalidOperationException("Unable to generate unique purchase order number.");
    }

    private static bool HasDuplicateProducts(IEnumerable<Guid> productIds)
        => productIds.GroupBy(x => x).Any(x => x.Count() > 1);

    private PurchaseOrderDTO MapPurchaseOrderSummary(PurchaseOrder purchaseOrder)
    {
        var dto = mapper.Map<PurchaseOrderDTO>(purchaseOrder);
        dto.Lines = null;
        return dto;
    }

}
