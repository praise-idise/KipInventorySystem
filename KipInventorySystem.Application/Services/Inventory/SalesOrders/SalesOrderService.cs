using Hangfire;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;
using KipInventorySystem.Application.Services.Inventory.SalesOrders.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.SalesOrders;

public class SalesOrderService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IDocumentNumberGenerator documentNumberGenerator,
    IUserContext userContext,
    IMapper mapper,
    ILogger<SalesOrderService> logger) : ISalesOrderService
{
    public Task<ServiceResponse<SalesOrderDto>> CreateDraftAsync(
        CreateSalesOrderDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "sales-order-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("salesOrder.createDraft", async _ =>
            {
                // A draft must be tied to a warehouse that can actually cover every requested line.
                var validation = await ValidateWarehouseAndProductsAsync(
                    request.WarehouseId,
                    [.. request.Lines.Select(x => new WarehouseProductRequirement(x.ProductId, x.QuantityOrdered))],
                    token);

                if (validation is not null)
                {
                    return validation;
                }

                if (HasDuplicateProducts(request.Lines.Select(x => x.ProductId)))
                {
                    return ServiceResponse<SalesOrderDto>.BadRequest("Duplicate products are not allowed in a sales order.");
                }

                // Either reuse an existing customer or stage a new one inside the same transaction.
                var customerIdResult = await ResolveCustomerIdAsync(request.CustomerId, request.Customer, token);
                if (!customerIdResult.Succeeded)
                {
                    return ServiceResponse<SalesOrderDto>.BadRequest(customerIdResult.Message!);
                }

                var salesOrder = mapper.Map<SalesOrder>(request);
                salesOrder.CustomerId = customerIdResult.Data!.Value;
                salesOrder.SalesOrderNumber = await GenerateUniqueSalesOrderNumberAsync(token);
                salesOrder.Status = SalesOrderStatus.Draft;
                salesOrder.OrderedAt = DateTime.UtcNow;
                salesOrder.CreatedAt = DateTime.UtcNow;
                salesOrder.UpdatedAt = DateTime.UtcNow;

                var salesOrderRepo = unitOfWork.Repository<SalesOrder>();
                var lineRepo = unitOfWork.Repository<SalesOrderLine>();
                await salesOrderRepo.AddAsync(salesOrder, token);

                // Draft lines start unfulfilled and are attached after the sales order id is generated.
                var lines = mapper.Map<List<SalesOrderLine>>(request.Lines);
                foreach (var line in lines)
                {
                    line.SalesOrderId = salesOrder.SalesOrderId;
                    line.QuantityFulfilled = 0;
                    line.CreatedAt = DateTime.UtcNow;
                    line.UpdatedAt = DateTime.UtcNow;
                }

                await lineRepo.AddRangeAsync(lines, token);
                salesOrder.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=SalesOrder, entityId={EntityId}, status={Status}",
                    "CreateSalesOrderDraft",
                    userContext.GetCurrentUser().UserId,
                    salesOrder.SalesOrderId,
                    salesOrder.Status);

                return ServiceResponse<SalesOrderDto>.Created(
                    mapper.Map<SalesOrderDto>(salesOrder),
                    "Sales order draft created.");
            }, token),
            cancellationToken);
    }

    public Task<ServiceResponse<SalesOrderDto>> UpdateDraftAsync(
        Guid salesOrderId,
        UpdateSalesOrderDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        return transactionRunner.ExecuteSerializableAsync("salesOrder.updateDraft", async token =>
        {
            var salesOrderRepo = unitOfWork.Repository<SalesOrder>();
            var lineRepo = unitOfWork.Repository<SalesOrderLine>();

            var salesOrder = await salesOrderRepo.GetByIdAsync(salesOrderId, token);
            if (salesOrder is null)
            {
                return ServiceResponse<SalesOrderDto>.NotFound("Sales order was not found.");
            }

            if (salesOrder.Status != SalesOrderStatus.Draft)
            {
                return ServiceResponse<SalesOrderDto>.Conflict("Only draft sales orders can be updated.");
            }

            var effectiveCustomerId = request.CustomerId ?? salesOrder.CustomerId;
            var effectiveWarehouseId = request.WarehouseId ?? salesOrder.WarehouseId;

            if (request.Lines is not null && HasDuplicateProducts(request.Lines.Select(x => x.ProductId)))
            {
                return ServiceResponse<SalesOrderDto>.BadRequest("Duplicate products are not allowed in a sales order.");
            }

            if (request.CustomerId.HasValue || request.WarehouseId.HasValue || request.Lines is not null)
            {
                List<WarehouseProductRequirement> lineRequirements;
                if (request.Lines is not null)
                {
                    lineRequirements = [.. request.Lines.Select(x => new WarehouseProductRequirement(x.ProductId, x.QuantityOrdered))];
                }
                else
                {
                    lineRequirements = [.. (await lineRepo.WhereAsync(x => x.SalesOrderId == salesOrderId, token))
                        .Select(x => new WarehouseProductRequirement(x.ProductId, x.QuantityOrdered))];
                }

                // Revalidate against the effective warehouse and the final set of lines after the update.
                var validation = await ValidateWarehouseAndProductsAsync(
                    effectiveWarehouseId,
                    lineRequirements,
                    token);

                if (validation is not null)
                {
                    return validation;
                }

                if (request.CustomerId.HasValue)
                {
                    var customerValidation = await ValidateCustomerAsync(effectiveCustomerId, token);
                    if (customerValidation is not null)
                    {
                        return customerValidation;
                    }
                }
            }

            if (request.CustomerId.HasValue)
            {
                salesOrder.CustomerId = request.CustomerId.Value;
            }

            if (request.WarehouseId.HasValue)
            {
                salesOrder.WarehouseId = request.WarehouseId.Value;
            }

            if (request.Notes is not null)
            {
                salesOrder.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            }

            salesOrder.UpdatedAt = DateTime.UtcNow;
            salesOrderRepo.Update(salesOrder);

            if (request.Lines is not null)
            {
                // Replacing draft lines is simpler and keeps reserved/fulfilled quantities consistent.
                var existingLines = await lineRepo.WhereAsync(x => x.SalesOrderId == salesOrderId, token);
                if (existingLines.Count > 0)
                {
                    lineRepo.RemoveRange(existingLines);
                }

                var newLines = mapper.Map<List<SalesOrderLine>>(request.Lines);
                foreach (var line in newLines)
                {
                    line.SalesOrderId = salesOrderId;
                    line.QuantityFulfilled = 0;
                    line.CreatedAt = DateTime.UtcNow;
                    line.UpdatedAt = DateTime.UtcNow;
                }

                await lineRepo.AddRangeAsync(newLines, token);
                salesOrder.Lines = newLines;
            }
            else
            {
                salesOrder.Lines = await lineRepo.WhereAsync(x => x.SalesOrderId == salesOrderId, token);
            }

            logger.LogInformation(
                "Inventory audit: operation={Operation}, actor={Actor}, entity=SalesOrder, entityId={EntityId}",
                "UpdateSalesOrderDraft",
                userContext.GetCurrentUser().UserId,
                salesOrder.SalesOrderId);

            return ServiceResponse<SalesOrderDto>.Success(
                mapper.Map<SalesOrderDto>(salesOrder),
                "Sales order draft updated.");
        }, cancellationToken);
    }

    public async Task<ServiceResponse<SalesOrderDto>> ConfirmAsync(
        Guid salesOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync<Guid, SalesOrderDto>(
            "sales-order-confirm",
            idempotencyKey,
            salesOrderId,
            token => transactionRunner.ExecuteSerializableAsync("salesOrder.confirm", async _ =>
            {
                var salesOrderRepo = unitOfWork.Repository<SalesOrder>();
                var lineRepo = unitOfWork.Repository<SalesOrderLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();

                var salesOrder = await salesOrderRepo.GetByIdAsync(salesOrderId, token);
                if (salesOrder is null)
                {
                    return ServiceResponse<SalesOrderDto>.NotFound("Sales order was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.SalesOrderId == salesOrderId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<SalesOrderDto>.BadRequest("Sales order has no lines.");
                }

                if (salesOrder.Status is SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled or SalesOrderStatus.Fulfilled)
                {
                    salesOrder.Lines = lines;
                    return ServiceResponse<SalesOrderDto>.Success(
                        mapper.Map<SalesOrderDto>(salesOrder),
                        "Sales order already confirmed.");
                }

                if (salesOrder.Status != SalesOrderStatus.Draft)
                {
                    return ServiceResponse<SalesOrderDto>.Conflict("Only draft sales orders can be confirmed.");
                }

                // Confirmation is the point where draft demand becomes reserved stock.
                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == salesOrder.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null || inventory.AvailableQuantity < line.QuantityOrdered)
                    {
                        return ServiceResponse<SalesOrderDto>.Conflict(
                            $"Insufficient available stock for product '{line.ProductId}'.");
                    }
                }

                foreach (var line in lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == salesOrder.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null)
                    {
                        continue;
                    }

                    inventory.ReservedQuantity += line.QuantityOrdered;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProducts.Add(line.ProductId);
                }

                // Once all reservations succeed, the document can move to confirmed.
                salesOrder.Status = SalesOrderStatus.Confirmed;
                salesOrder.ConfirmedAt = DateTime.UtcNow;
                salesOrder.UpdatedAt = DateTime.UtcNow;
                salesOrderRepo.Update(salesOrder);
                salesOrder.Lines = lines;
                affectedWarehouse = salesOrder.WarehouseId;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=SalesOrder, entityId={EntityId}, newStatus={NewStatus}",
                    "ConfirmSalesOrder",
                    userContext.GetCurrentUser().UserId,
                    salesOrder.SalesOrderId,
                    salesOrder.Status);

                return ServiceResponse<SalesOrderDto>.Success(
                    mapper.Map<SalesOrderDto>(salesOrder),
                    "Sales order confirmed.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public async Task<ServiceResponse<SalesOrderDto>> FulfillAsync(
        Guid salesOrderId,
        FulfillSalesOrderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var response = await idempotencyService.ExecuteAsync(
            "sales-order-fulfill",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("salesOrder.fulfill", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var salesOrderRepo = unitOfWork.Repository<SalesOrder>();
                var lineRepo = unitOfWork.Repository<SalesOrderLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var salesOrder = await salesOrderRepo.GetByIdAsync(salesOrderId, token);
                if (salesOrder is null)
                {
                    return ServiceResponse<SalesOrderDto>.NotFound("Sales order was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.SalesOrderId == salesOrderId, token);
                if (lines.Count == 0)
                {
                    return ServiceResponse<SalesOrderDto>.BadRequest("Sales order has no lines.");
                }

                if (salesOrder.Status == SalesOrderStatus.Fulfilled)
                {
                    salesOrder.Lines = lines;
                    return ServiceResponse<SalesOrderDto>.Success(
                        mapper.Map<SalesOrderDto>(salesOrder),
                        "Sales order already fulfilled.");
                }

                if (salesOrder.Status is not SalesOrderStatus.Confirmed and not SalesOrderStatus.PartiallyFulfilled)
                {
                    return ServiceResponse<SalesOrderDto>.Conflict("Only confirmed sales orders can be fulfilled.");
                }

                var requestLinesById = request.Lines.ToDictionary(x => x.SalesOrderLineId, x => x);
                var movements = new List<StockMovement>();

                foreach (var requestLine in request.Lines)
                {
                    var line = lines.FirstOrDefault(x => x.SalesOrderLineId == requestLine.SalesOrderLineId);
                    if (line is null)
                    {
                        return ServiceResponse<SalesOrderDto>.BadRequest(
                            $"Sales order line '{requestLine.SalesOrderLineId}' was not found.");
                    }

                    var remaining = line.QuantityOrdered - line.QuantityFulfilled;
                    if (requestLine.QuantityFulfilledNow > remaining)
                    {
                        return ServiceResponse<SalesOrderDto>.BadRequest(
                            $"Cannot fulfill {requestLine.QuantityFulfilledNow} units for line '{line.SalesOrderLineId}'. Remaining quantity is {remaining}.");
                    }
                }

                foreach (var line in lines)
                {
                    if (!requestLinesById.TryGetValue(line.SalesOrderLineId, out var requestLine))
                    {
                        continue;
                    }

                    // Fulfillment consumes only stock that was already reserved during confirmation.
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == salesOrder.WarehouseId && x.ProductId == line.ProductId,
                        token);

                    if (inventory is null || inventory.QuantityOnHand < requestLine.QuantityFulfilledNow || inventory.ReservedQuantity < requestLine.QuantityFulfilledNow)
                    {
                        return ServiceResponse<SalesOrderDto>.Conflict(
                            $"Insufficient reserved stock for product '{line.ProductId}'.");
                    }

                    var (unitCost, totalCost) = InventoryCosting.ApplyOutbound(inventory, requestLine.QuantityFulfilledNow);
                    inventory.ReservedQuantity -= requestLine.QuantityFulfilledNow;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);

                    line.QuantityFulfilled += requestLine.QuantityFulfilledNow;
                    line.UpdatedAt = DateTime.UtcNow;
                    lineRepo.Update(line);

                    movements.Add(new StockMovement
                    {
                        ProductId = line.ProductId,
                        WarehouseId = salesOrder.WarehouseId,
                        MovementType = StockMovementType.Issue,
                        Quantity = requestLine.QuantityFulfilledNow,
                        UnitCost = unitCost,
                        TotalCost = totalCost,
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.SalesOrder,
                        ReferenceId = salesOrder.SalesOrderId,
                        Creator = currentUser.FullName,
                        CreatorId = currentUser.UserId,
                        Notes = string.IsNullOrWhiteSpace(request.Notes) ? salesOrder.Notes : request.Notes!.Trim(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                if (movements.Count > 0)
                {
                    await movementRepo.AddRangeAsync(movements, token);
                }

                // The overall order status follows the most complete line state after this batch.
                salesOrder.Status = lines.All(x => x.QuantityFulfilled >= x.QuantityOrdered)
                    ? SalesOrderStatus.Fulfilled
                    : SalesOrderStatus.PartiallyFulfilled;
                salesOrder.FulfilledAt = salesOrder.Status == SalesOrderStatus.Fulfilled ? DateTime.UtcNow : salesOrder.FulfilledAt;
                salesOrder.UpdatedAt = DateTime.UtcNow;
                salesOrderRepo.Update(salesOrder);
                salesOrder.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=SalesOrder, entityId={EntityId}, newStatus={NewStatus}",
                    "FulfillSalesOrder",
                    currentUser.UserId,
                    salesOrder.SalesOrderId,
                    salesOrder.Status);

                return ServiceResponse<SalesOrderDto>.Success(
                    mapper.Map<SalesOrderDto>(salesOrder),
                    "Sales order fulfillment applied.");
            }, token),
            cancellationToken);

        return response;
    }

    public async Task<ServiceResponse<SalesOrderDto>> CancelAsync(
        Guid salesOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();
        Guid? affectedWarehouse = null;

        var response = await idempotencyService.ExecuteAsync<Guid, SalesOrderDto>(
            "sales-order-cancel",
            idempotencyKey,
            salesOrderId,
            token => transactionRunner.ExecuteSerializableAsync("salesOrder.cancel", async _ =>
            {
                var salesOrderRepo = unitOfWork.Repository<SalesOrder>();
                var lineRepo = unitOfWork.Repository<SalesOrderLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();

                var salesOrder = await salesOrderRepo.GetByIdAsync(salesOrderId, token);
                if (salesOrder is null)
                {
                    return ServiceResponse<SalesOrderDto>.NotFound("Sales order was not found.");
                }

                var lines = await lineRepo.WhereAsync(x => x.SalesOrderId == salesOrderId, token);
                salesOrder.Lines = lines;

                if (salesOrder.Status == SalesOrderStatus.Cancelled)
                {
                    return ServiceResponse<SalesOrderDto>.Success(
                        mapper.Map<SalesOrderDto>(salesOrder),
                        "Sales order already cancelled.");
                }

                if (salesOrder.Status == SalesOrderStatus.Fulfilled)
                {
                    return ServiceResponse<SalesOrderDto>.Conflict("Fulfilled sales orders cannot be cancelled.");
                }

                if (salesOrder.Status is SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled)
                {
                    // Cancelling a reserved order releases only the remaining unfulfilled quantity.
                    foreach (var line in lines)
                    {
                        var remainingToRelease = line.QuantityOrdered - line.QuantityFulfilled;
                        if (remainingToRelease <= 0)
                        {
                            continue;
                        }

                        var inventory = await inventoryRepo.FindAsync(
                            x => x.WarehouseId == salesOrder.WarehouseId && x.ProductId == line.ProductId,
                            token);

                        if (inventory is null)
                        {
                            continue;
                        }

                        inventory.ReservedQuantity = Math.Max(0, inventory.ReservedQuantity - remainingToRelease);
                        inventory.UpdatedAt = DateTime.UtcNow;
                        inventoryRepo.Update(inventory);
                        affectedProducts.Add(line.ProductId);
                    }

                    affectedWarehouse = salesOrder.WarehouseId;
                }

                salesOrder.Status = SalesOrderStatus.Cancelled;
                salesOrder.UpdatedAt = DateTime.UtcNow;
                salesOrderRepo.Update(salesOrder);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=SalesOrder, entityId={EntityId}, newStatus={NewStatus}",
                    "CancelSalesOrder",
                    userContext.GetCurrentUser().UserId,
                    salesOrder.SalesOrderId,
                    salesOrder.Status);

                return ServiceResponse<SalesOrderDto>.Success(
                    mapper.Map<SalesOrderDto>(salesOrder),
                    "Sales order cancelled.");
            }, token),
            cancellationToken);

        EnqueueLowStockChecks(response.Succeeded, affectedWarehouse, affectedProducts);
        return response;
    }

    public async Task<ServiceResponse<SalesOrderDto>> GetByIdAsync(Guid salesOrderId, CancellationToken cancellationToken = default)
    {
        var salesOrder = await unitOfWork.Repository<SalesOrder>().GetByIdAsync(
            salesOrderId,
            query => query
                .Include(x => x.Customer)
                .Include(x => x.Lines)
                .ThenInclude(x => x.Product),
            cancellationToken);

        if (salesOrder is null)
        {
            return ServiceResponse<SalesOrderDto>.NotFound("Sales order was not found.");
        }

        return ServiceResponse<SalesOrderDto>.Success(mapper.Map<SalesOrderDto>(salesOrder));
    }

    public async Task<ServiceResponse<PaginationResult<SalesOrderDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var salesOrders = await unitOfWork.Repository<SalesOrder>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.OrderedAt),
            cancellationToken: cancellationToken,
            include: query => query.Include(x => x.Customer));

        var response = new PaginationResult<SalesOrderDto>
        {
            Records = salesOrders.Records.Select(x => mapper.Map<SalesOrderDto>(x)).ToList(),
            TotalRecords = salesOrders.TotalRecords,
            PageSize = salesOrders.PageSize,
            CurrentPage = salesOrders.CurrentPage
        };

        return ServiceResponse<PaginationResult<SalesOrderDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<SalesOrderDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var pattern = $"%{searchTerm.Trim()}%";
        var salesOrders = await unitOfWork.Repository<SalesOrder>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.OrderedAt),
            x => EF.Functions.ILike(x.SalesOrderNumber, pattern) ||
                 EF.Functions.ILike(x.Customer.Name, pattern) ||
                 (x.Notes != null && EF.Functions.ILike(x.Notes, pattern)),
            cancellationToken,
            include: query => query.Include(x => x.Customer));

        var response = new PaginationResult<SalesOrderDto>
        {
            Records = salesOrders.Records.Select(x => mapper.Map<SalesOrderDto>(x)).ToList(),
            TotalRecords = salesOrders.TotalRecords,
            PageSize = salesOrders.PageSize,
            CurrentPage = salesOrders.CurrentPage
        };

        return ServiceResponse<PaginationResult<SalesOrderDto>>.Success(response);
    }

    private async Task<ServiceResponse<SalesOrderDto>?> ValidateWarehouseAndProductsAsync(
        Guid warehouseId,
        List<WarehouseProductRequirement> lineRequirements,
        CancellationToken cancellationToken)
    {
        var warehouse = await unitOfWork.Repository<Warehouse>().GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return ServiceResponse<SalesOrderDto>.BadRequest("Warehouse was not found.");
        }

        var productRepo = unitOfWork.Repository<Product>();
        var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
        foreach (var lineRequirement in lineRequirements
                     .GroupBy(x => x.ProductId)
                     .Select(x => new WarehouseProductRequirement(x.Key, x.Sum(y => y.QuantityOrdered))))
        {
            // Duplicate line products are already blocked earlier, but grouping keeps validation safe.
            var product = await productRepo.GetByIdAsync(lineRequirement.ProductId, cancellationToken);
            if (product is null)
            {
                return ServiceResponse<SalesOrderDto>.BadRequest($"Product '{lineRequirement.ProductId}' was not found.");
            }

            var inventory = await inventoryRepo.FindAsync(
                x => x.WarehouseId == warehouseId && x.ProductId == lineRequirement.ProductId,
                cancellationToken);

            if (inventory is null)
            {
                return ServiceResponse<SalesOrderDto>.BadRequest(
                    $"Product '{product.Name}' is not stocked in warehouse '{warehouse.Name}'.");
            }

            if (inventory.AvailableQuantity < lineRequirement.QuantityOrdered)
            {
                return ServiceResponse<SalesOrderDto>.Conflict(
                    $"Warehouse '{warehouse.Name}' does not have enough available stock for product '{product.Name}'.");
            }
        }

        return null;
    }

    private sealed record WarehouseProductRequirement(Guid ProductId, int QuantityOrdered);

    private async Task<ServiceResponse<SalesOrderDto>?> ValidateCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await unitOfWork.Repository<Customer>().GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return ServiceResponse<SalesOrderDto>.BadRequest("Customer was not found.");
        }

        return null;
    }

    private async Task<ServiceResponse<Guid?>> ResolveCustomerIdAsync(
        Guid? customerId,
        CreateCustomerRequest? customerRequest,
        CancellationToken cancellationToken)
    {
        if (customerId.HasValue)
        {
            var existingCustomer = await unitOfWork.Repository<Customer>().GetByIdAsync(customerId.Value, cancellationToken);
            if (existingCustomer is null)
            {
                return ServiceResponse<Guid?>.BadRequest("Customer was not found.");
            }

            return ServiceResponse<Guid?>.Success(existingCustomer.CustomerId);
        }

        if (customerRequest is null)
        {
            return ServiceResponse<Guid?>.BadRequest("Select an existing customer or provide a new customer.");
        }

        // Inline customer creation is allowed for convenience, but email uniqueness still applies.
        var customerRepo = unitOfWork.Repository<Customer>();
        var normalizedEmail = string.IsNullOrWhiteSpace(customerRequest.Email)
            ? null
            : customerRequest.Email.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var existingEmailCustomer = await customerRepo.FindAsync(x => x.Email == normalizedEmail, cancellationToken);
            if (existingEmailCustomer is not null)
            {
                return ServiceResponse<Guid?>.BadRequest($"A customer with email '{normalizedEmail}' already exists. Select that customer instead.");
            }
        }

        var customer = mapper.Map<Customer>(customerRequest);
        customer.CreatedAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;
        await customerRepo.AddAsync(customer, cancellationToken);

        return ServiceResponse<Guid?>.Success(customer.CustomerId);
    }

    private async Task<string> GenerateUniqueSalesOrderNumberAsync(CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<SalesOrder>();
        // Retry a few times in-memory before failing the transaction on a number collision.
        for (var i = 0; i < 5; i++)
        {
            var number = documentNumberGenerator.GenerateSalesOrderNumber();
            var exists = await repo.ExistsAsync(x => x.SalesOrderNumber == number, cancellationToken);
            if (!exists)
            {
                return number;
            }
        }

        throw new InvalidOperationException("Unable to generate unique sales order number.");
    }

    private void EnqueueLowStockChecks(bool succeeded, Guid? warehouseId, IEnumerable<Guid> productIds)
    {
        if (!succeeded || warehouseId is null)
        {
            return;
        }

        // Recheck only the products affected by reservation or release work.
        foreach (var productId in productIds.Distinct())
        {
            BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                "default",
                jobs => jobs.EvaluateLowStockAsync(warehouseId.Value, productId, default));
        }
    }

    private static bool HasDuplicateProducts(IEnumerable<Guid> productIds)
        => productIds.GroupBy(x => x).Any(x => x.Count() > 1);
}
