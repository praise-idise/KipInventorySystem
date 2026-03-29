using Hangfire;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.OpeningBalances.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.OpeningBalances;

public class OpeningBalanceService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IDocumentNumberGenerator documentNumberGenerator,
    IUserContext userContext,
    IMapper mapper,
    ILogger<OpeningBalanceService> logger) : IOpeningBalanceService
{
    public async Task<ServiceResponse<OpeningBalanceDto>> CreateAsync(
        CreateOpeningBalanceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var affectedProducts = new HashSet<Guid>();

        var response = await idempotencyService.ExecuteAsync(
            "opening-balance-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("openingBalance.create", async _ =>
            {
                var currentUser = userContext.GetCurrentUser();
                var warehouseRepo = unitOfWork.Repository<Warehouse>();
                var productRepo = unitOfWork.Repository<Product>();
                var openingBalanceRepo = unitOfWork.Repository<OpeningBalance>();
                var lineRepo = unitOfWork.Repository<OpeningBalanceLine>();
                var inventoryRepo = unitOfWork.Repository<WarehouseInventory>();
                var movementRepo = unitOfWork.Repository<StockMovement>();

                var warehouse = await warehouseRepo.GetByIdAsync(request.WarehouseId, token);
                if (warehouse is null)
                {
                    return ServiceResponse<OpeningBalanceDto>.BadRequest("Warehouse was not found.");
                }

                foreach (var line in request.Lines)
                {
                    var product = await productRepo.GetByIdAsync(line.ProductId, token);
                    if (product is null)
                    {
                        return ServiceResponse<OpeningBalanceDto>.BadRequest($"Product '{line.ProductId}' was not found.");
                    }
                }

                var openingBalance = mapper.Map<OpeningBalance>(request);
                openingBalance.OpeningBalanceNumber = await GenerateUniqueOpeningBalanceNumberAsync(token);
                openingBalance.AppliedAt = DateTime.UtcNow;
                openingBalance.CreatedAt = DateTime.UtcNow;
                openingBalance.UpdatedAt = DateTime.UtcNow;

                await openingBalanceRepo.AddAsync(openingBalance, token);

                var lines = new List<OpeningBalanceLine>();
                var movements = new List<StockMovement>();

                foreach (var lineRequest in request.Lines)
                {
                    var inventory = await inventoryRepo.FindAsync(
                        x => x.WarehouseId == request.WarehouseId && x.ProductId == lineRequest.ProductId,
                        token);

                    var hasStockHistory = await movementRepo.ExistsAsync(
                        x => x.WarehouseId == request.WarehouseId && x.ProductId == lineRequest.ProductId,
                        token);

                    if (hasStockHistory)
                    {
                        return ServiceResponse<OpeningBalanceDto>.Conflict(
                            $"Opening balance cannot be created for product '{lineRequest.ProductId}' because stock history already exists in this warehouse.");
                    }

                    if (inventory is null)
                    {
                        inventory = new WarehouseInventory
                        {
                            WarehouseId = request.WarehouseId,
                            ProductId = lineRequest.ProductId,
                            QuantityOnHand = 0,
                            ReservedQuantity = 0,
                            AverageUnitCost = 0m,
                            InventoryValue = 0m,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await inventoryRepo.AddAsync(inventory, token);
                    }
                    else if (inventory.QuantityOnHand != 0 ||
                             inventory.ReservedQuantity != 0 ||
                             inventory.AverageUnitCost != 0m ||
                             inventory.InventoryValue != 0m)
                    {
                        return ServiceResponse<OpeningBalanceDto>.Conflict(
                            $"Opening balance requires a zero starting position. Product '{lineRequest.ProductId}' already has inventory in this warehouse.");
                    }

                    var lineUnitCost = InventoryCosting.Round(lineRequest.UnitCost);
                    var (appliedUnitCost, totalCost) = InventoryCosting.ApplyInbound(
                        inventory,
                        lineRequest.Quantity,
                        lineUnitCost);

                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventoryRepo.Update(inventory);
                    affectedProducts.Add(lineRequest.ProductId);

                    var line = new OpeningBalanceLine
                    {
                        OpeningBalanceId = openingBalance.OpeningBalanceId,
                        ProductId = lineRequest.ProductId,
                        Quantity = lineRequest.Quantity,
                        UnitCost = appliedUnitCost,
                        TotalCost = totalCost,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    lines.Add(line);

                    movements.Add(new StockMovement
                    {
                        ProductId = lineRequest.ProductId,
                        WarehouseId = request.WarehouseId,
                        MovementType = StockMovementType.OpeningBalance,
                        Quantity = lineRequest.Quantity,
                        UnitCost = appliedUnitCost,
                        TotalCost = totalCost,
                        OccurredAt = DateTime.UtcNow,
                        ReferenceType = StockMovementReferenceType.OpeningBalance,
                        ReferenceId = openingBalance.OpeningBalanceId,
                        Creator = currentUser.FullName,
                        CreatorId = currentUser.UserId,
                        Notes = openingBalance.Notes,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await lineRepo.AddRangeAsync(lines, token);
                await movementRepo.AddRangeAsync(movements, token);
                openingBalance.Lines = lines;

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=OpeningBalance, entityId={EntityId}, movementCount={MovementCount}",
                    "CreateOpeningBalance",
                    currentUser.UserId,
                    openingBalance.OpeningBalanceId,
                    movements.Count);

                return ServiceResponse<OpeningBalanceDto>.Created(
                    mapper.Map<OpeningBalanceDto>(openingBalance),
                    "Opening balance created.");
            }, token),
            cancellationToken);

        if (response.Succeeded)
        {
            foreach (var productId in affectedProducts)
            {
                BackgroundJob.Enqueue<ILowStockBackgroundJobs>(
                    "default",
                    jobs => jobs.EvaluateLowStockAsync(request.WarehouseId, productId, default));
            }
        }

        return response;
    }

    public async Task<ServiceResponse<OpeningBalanceDto>> GetByIdAsync(
        Guid openingBalanceId,
        CancellationToken cancellationToken = default)
    {
        var openingBalanceRepo = unitOfWork.Repository<OpeningBalance>();
        var lineRepo = unitOfWork.Repository<OpeningBalanceLine>();
        var openingBalance = await openingBalanceRepo.GetByIdAsync(openingBalanceId, cancellationToken);
        if (openingBalance is null)
        {
            return ServiceResponse<OpeningBalanceDto>.NotFound("Opening balance was not found.");
        }

        openingBalance.Lines = await lineRepo.WhereAsync(x => x.OpeningBalanceId == openingBalanceId, cancellationToken);
        return ServiceResponse<OpeningBalanceDto>.Success(mapper.Map<OpeningBalanceDto>(openingBalance));
    }

    public async Task<ServiceResponse<PaginationResult<OpeningBalanceDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var openingBalanceRepo = unitOfWork.Repository<OpeningBalance>();
        var pagedOpeningBalances = await openingBalanceRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.AppliedAt),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<OpeningBalanceDto>
        {
            Records = pagedOpeningBalances.Records.Select(MapSummaryOpeningBalance).ToList(),
            TotalRecords = pagedOpeningBalances.TotalRecords,
            PageSize = pagedOpeningBalances.PageSize,
            CurrentPage = pagedOpeningBalances.CurrentPage
        };

        return ServiceResponse<PaginationResult<OpeningBalanceDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<OpeningBalanceDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var openingBalanceRepo = unitOfWork.Repository<OpeningBalance>();
        var pagedOpeningBalances = await openingBalanceRepo.GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.AppliedAt),
            x => x.OpeningBalanceNumber.ToLower().Contains(term) ||
                 (x.Notes != null && x.Notes.ToLower().Contains(term)),
            cancellationToken);

        var response = new PaginationResult<OpeningBalanceDto>
        {
            Records = pagedOpeningBalances.Records.Select(MapSummaryOpeningBalance).ToList(),
            TotalRecords = pagedOpeningBalances.TotalRecords,
            PageSize = pagedOpeningBalances.PageSize,
            CurrentPage = pagedOpeningBalances.CurrentPage
        };

        return ServiceResponse<PaginationResult<OpeningBalanceDto>>.Success(response);
    }

    private async Task<string> GenerateUniqueOpeningBalanceNumberAsync(CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<OpeningBalance>();
        for (var i = 0; i < 5; i++)
        {
            var number = documentNumberGenerator.GenerateOpeningBalanceNumber();
            var exists = await repo.ExistsAsync(x => x.OpeningBalanceNumber == number, cancellationToken);
            if (!exists)
            {
                return number;
            }
        }

        throw new InvalidOperationException("Unable to generate unique opening balance number.");
    }

    private OpeningBalanceDto MapSummaryOpeningBalance(OpeningBalance openingBalance)
    {
        var dto = mapper.Map<OpeningBalanceDto>(openingBalance);
        dto.Lines = null;
        return dto;
    }
}
