using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Warehouses;

public class WarehouseService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IUserContext userContext,
    IMapper mapper,
    ILogger<WarehouseService> logger) : IWarehouseService
{
    public async Task<ServiceResponse<WarehouseDto>> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseRepo = unitOfWork.Repository<Warehouse>();
        var counterRepo = unitOfWork.Repository<WarehouseCodeCounter>();
        var normalizedName = request.Name.Trim();

        if (await WarehouseNameExistsAsync(warehouseRepo, normalizedName, cancellationToken))
        {
            return ServiceResponse<WarehouseDto>.Conflict(
                $"A warehouse with name '{normalizedName}' already exists.");
        }

        return await transactionRunner.ExecuteSerializableAsync("warehouse.create", async token =>
        {
            if (await WarehouseNameExistsAsync(warehouseRepo, normalizedName, token))
            {
                return ServiceResponse<WarehouseDto>.Conflict(
                    $"A warehouse with name '{normalizedName}' already exists.");
            }

            var warehouse = mapper.Map<Warehouse>(request);
            warehouse.Code = await GenerateNextWarehouseCodeAsync(counterRepo, warehouse.State, token);
            warehouse.CreatedAt = DateTime.UtcNow;
            warehouse.UpdatedAt = DateTime.UtcNow;

            await warehouseRepo.AddAsync(warehouse, token);

            logger.LogInformation(
                "Inventory audit: operation={Operation}, actor={Actor}, entity=Warehouse, entityId={EntityId}",
                "CreateWarehouse",
                userContext.GetCurrentUser().UserId,
                warehouse.WarehouseId);

            return ServiceResponse<WarehouseDto>.Created(
                mapper.Map<WarehouseDto>(warehouse),
                "Warehouse created successfully.");
        }, cancellationToken);
    }

    public async Task<ServiceResponse<WarehouseDto>> UpdateAsync(
        Guid warehouseId,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseRepo = unitOfWork.Repository<Warehouse>();
        var counterRepo = unitOfWork.Repository<WarehouseCodeCounter>();
        var warehouse = await warehouseRepo.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return ServiceResponse<WarehouseDto>.NotFound("Warehouse was not found.");
        }

        if (request.Name is not null)
        {
            var normalizedName = request.Name.Trim();
            var duplicateNameExists = await warehouseRepo.ExistsAsync(
                x => x.WarehouseId != warehouseId && x.Name == normalizedName,
                cancellationToken);

            if (duplicateNameExists)
            {
                return ServiceResponse<WarehouseDto>.Conflict(
                    $"A warehouse with name '{normalizedName}' already exists.");
            }

            warehouse.Name = normalizedName;
        }

        if (request.State is not null)
        {
            var normalizedState = request.State.Trim();
            if (!string.Equals(warehouse.State, normalizedState, StringComparison.Ordinal))
            {
                warehouse.State = normalizedState;
                warehouse.Code = await GenerateNextWarehouseCodeAsync(
                    counterRepo,
                    warehouse.State,
                    cancellationToken);
            }
        }

        if (request.Location is not null)
        {
            warehouse.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        }

        if (request.CapacityUnits.HasValue)
        {
            warehouse.CapacityUnits = request.CapacityUnits.Value;
        }

        if (request.IsActive.HasValue)
        {
            warehouse.IsActive = request.IsActive.Value;
        }

        warehouse.UpdatedAt = DateTime.UtcNow;
        warehouseRepo.Update(warehouse);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            if (request.Name is not null)
            {
                return ServiceResponse<WarehouseDto>.Conflict(
                    $"A warehouse with name '{request.Name.Trim()}' already exists.");
            }

            throw;
        }

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Warehouse, entityId={EntityId}",
            "UpdateWarehouse",
            userContext.GetCurrentUser().UserId,
            warehouse.WarehouseId);

        return ServiceResponse<WarehouseDto>.Success(
            mapper.Map<WarehouseDto>(warehouse),
            "Warehouse updated successfully.");
    }

    public async Task<ServiceResponse> SoftDeleteAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouseRepo = unitOfWork.Repository<Warehouse>();
        var warehouse = await warehouseRepo.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return ServiceResponse.NotFound("Warehouse was not found.");
        }

        warehouse.IsDeleted = true;
        warehouse.IsActive = false;
        warehouse.DeletedAt = DateTime.UtcNow;
        warehouse.UpdatedAt = DateTime.UtcNow;
        warehouseRepo.Update(warehouse);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Warehouse, entityId={EntityId}",
            "DeleteWarehouse",
            userContext.GetCurrentUser().UserId,
            warehouse.WarehouseId);

        return ServiceResponse.Success("Warehouse deleted successfully.");
    }

    public async Task<ServiceResponse<WarehouseDto>> GetByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouse = await unitOfWork.Repository<Warehouse>().GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return ServiceResponse<WarehouseDto>.NotFound("Warehouse was not found.");
        }

        return ServiceResponse<WarehouseDto>.Success(mapper.Map<WarehouseDto>(warehouse));
    }

    public async Task<ServiceResponse<List<WarehouseInventoryItemDto>>> GetInventoryAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await unitOfWork.Repository<Warehouse>().GetByIdAsync(
            warehouseId,
            query => query
                .Include(x => x.InventoryItems)
                .ThenInclude(x => x.Product),
            cancellationToken);

        if (warehouse is null)
        {
            return ServiceResponse<List<WarehouseInventoryItemDto>>.NotFound("Warehouse was not found.");
        }

        var items = warehouse.InventoryItems
            .OrderBy(x => x.Product.Name)
            .ThenBy(x => x.Product.Sku)
            .Select(x => mapper.Map<WarehouseInventoryItemDto>(x))
            .ToList();

        return ServiceResponse<List<WarehouseInventoryItemDto>>.Success(items);
    }

    public async Task<ServiceResponse<PaginationResult<WarehouseDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var warehouses = await unitOfWork.Repository<Warehouse>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<WarehouseDto>
        {
            Records = [.. warehouses.Records.Select(x => mapper.Map<WarehouseDto>(x))],
            TotalRecords = warehouses.TotalRecords,
            PageSize = warehouses.PageSize,
            CurrentPage = warehouses.CurrentPage
        };

        return ServiceResponse<PaginationResult<WarehouseDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<WarehouseDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var warehouses = await unitOfWork.Repository<Warehouse>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            x => x.Name.ToLower().Contains(term) ||
                 x.Code.ToLower().Contains(term) ||
                 (x.Location != null && x.Location.ToLower().Contains(term)),
            cancellationToken);

        var response = new PaginationResult<WarehouseDto>
        {
            Records = [.. warehouses.Records.Select(x => mapper.Map<WarehouseDto>(x))],
            TotalRecords = warehouses.TotalRecords,
            PageSize = warehouses.PageSize,
            CurrentPage = warehouses.CurrentPage
        };

        return ServiceResponse<PaginationResult<WarehouseDto>>.Success(response);
    }

    private static async Task<string> GenerateNextWarehouseCodeAsync(
        IBaseRepository<WarehouseCodeCounter> counterRepo,
        string state,
        CancellationToken cancellationToken)
    {
        var stateCode = NormalizeStateCode(state);
        var existingCounter = await counterRepo.FindAsync(x => x.StateCode == stateCode, cancellationToken);
        var counterExists = existingCounter is not null;
        var counter = existingCounter;

        if (!counterExists)
        {
            counter = new WarehouseCodeCounter
            {
                StateCode = stateCode,
                LastNumber = 0
            };
            await counterRepo.AddAsync(counter, cancellationToken);
        }

        counter!.LastNumber += 1;
        if (counterExists)
        {
            counterRepo.Update(counter);
        }

        return $"WH-{stateCode}-{counter.LastNumber:000}";
    }

    private static string NormalizeStateCode(string state)
    {
        return new string([.. state.Where(char.IsLetterOrDigit)])
            .ToUpperInvariant()[..3];
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        if (exception is DbUpdateException dbUpdateException)
        {
            if (TryGetSqlState(dbUpdateException.InnerException, out var sqlState) &&
                sqlState == "23505")
            {
                return true;
            }

            var message = dbUpdateException.InnerException?.Message ?? dbUpdateException.Message;
            return message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
        }

        return exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException);
    }

    private static bool TryGetSqlState(Exception? exception, out string? sqlState)
    {
        sqlState = exception?.GetType().GetProperty("SqlState")?.GetValue(exception) as string;
        return !string.IsNullOrWhiteSpace(sqlState);
    }

    private static Task<bool> WarehouseNameExistsAsync(
        IBaseRepository<Warehouse> warehouseRepo,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        return warehouseRepo.ExistsAsync(
            x => x.Name == normalizedName,
            cancellationToken);
    }
}
