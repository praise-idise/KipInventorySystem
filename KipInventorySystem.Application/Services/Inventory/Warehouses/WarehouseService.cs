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
    IUserContext userContext,
    IMapper mapper,
    ILogger<WarehouseService> logger) : IWarehouseService
{
    private const int MaxCreateAttempts = 3;

    public async Task<ServiceResponse<WarehouseDto>> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseRepo = unitOfWork.Repository<Warehouse>();

         // Attempt to create the warehouse with a unique code, retrying if a collision occurs
        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var warehouse = mapper.Map<Warehouse>(request);
            warehouse.Code = await GenerateNextWarehouseCodeAsync(warehouseRepo, warehouse.State, cancellationToken);
            warehouse.CreatedAt = DateTime.UtcNow;
            warehouse.UpdatedAt = DateTime.UtcNow;

            try
            {
                await warehouseRepo.AddAsync(warehouse, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=Warehouse, entityId={EntityId}",
                    "CreateWarehouse",
                    userContext.GetCurrentUser().UserId,
                    warehouse.WarehouseId);

                return ServiceResponse<WarehouseDto>.Created(
                    mapper.Map<WarehouseDto>(warehouse),
                    "Warehouse created successfully.");
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxCreateAttempts)
            {
                warehouseRepo.Remove(warehouse);
                logger.LogWarning(
                    ex,
                    "Warehouse code generation collision on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    attempt,
                    MaxCreateAttempts);
            }
        }

        return ServiceResponse<WarehouseDto>.Conflict(
            "Unable to generate a unique warehouse code. Please retry.");
    }

    public async Task<ServiceResponse<WarehouseDto>> UpdateAsync(
        Guid warehouseId,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseRepo = unitOfWork.Repository<Warehouse>();
        var warehouse = await warehouseRepo.GetByIdAsync(warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return ServiceResponse<WarehouseDto>.NotFound("Warehouse was not found.");
        }

        if (request.Name is not null)
        {
            warehouse.Name = request.Name.Trim();
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
        IBaseRepository<Warehouse> warehouseRepo,
        string state,
        CancellationToken cancellationToken)
    {
        var stateCode = NormalizeStateCode(state);
        var prefix = $"WH-{stateCode}-";
        var matching = await warehouseRepo.WhereIncludingDeletedAsync(
            x => x.Code.StartsWith(prefix),
            cancellationToken);

        var maxSequence = matching
            .Select(x => ParseSequence(x.Code, prefix))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxSequence + 1:000}";
    }

    private static int? ParseSequence(string code, string prefix)
    {
        if (!code.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var suffix = code[prefix.Length..];
        return int.TryParse(suffix, out var value) ? value : null;
    }

    private static string NormalizeStateCode(string state)
    {
        var cleaned = new string([.. state.Where(char.IsLetterOrDigit)])
            .ToUpperInvariant();

        if (cleaned.Length >= 3)
        {
            return cleaned[..3];
        }

        return cleaned.PadRight(3, 'X');
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
}
