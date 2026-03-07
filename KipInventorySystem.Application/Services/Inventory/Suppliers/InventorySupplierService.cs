using KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Suppliers;

public class InventorySupplierService(
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMapper mapper,
    ILogger<InventorySupplierService> logger) : IInventorySupplierService
{
    public async Task<ServiceResponse<SupplierDto>> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplierRepo = unitOfWork.Repository<Supplier>();
        var newSupplier = mapper.Map<Supplier>(request);

        var existing = await supplierRepo.ExistsAsync(
            x => x.Name == newSupplier.Name || x.Email == newSupplier.Email,
            cancellationToken);

        if (existing)
        {
            return ServiceResponse<SupplierDto>.Conflict(
                $"A supplier with name '{newSupplier.Name}' or email '{newSupplier.Email}' already exists.");
        }

        var deletedExisting = (await supplierRepo.WhereIncludingDeletedAsync(
            x => x.Name == newSupplier.Name,
            cancellationToken)).Any(x => x.IsDeleted);

        if (deletedExisting)
        {
            return ServiceResponse<SupplierDto>.Conflict(
                $"Supplier name '{newSupplier.Name}' is reserved by a soft-deleted supplier.");
        }

        var supplier = newSupplier;
        supplier.CreatedAt = DateTime.UtcNow;
        supplier.UpdatedAt = DateTime.UtcNow;

        await supplierRepo.AddAsync(supplier, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Supplier, entityId={EntityId}",
            "CreateSupplier",
            userContext.GetCurrentUser().UserId,
            supplier.SupplierId);

        return ServiceResponse<SupplierDto>.Created(
            mapper.Map<SupplierDto>(supplier),
            "Supplier created successfully.");
    }

    public async Task<ServiceResponse<SupplierDto>> UpdateAsync(
        Guid supplierId,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplierRepo = unitOfWork.Repository<Supplier>();
        var supplier = await supplierRepo.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return ServiceResponse<SupplierDto>.NotFound("Supplier was not found.");
        }

        if (request.Name is not null)
        {
            var normalizedName = request.Name.Trim();
            var duplicate = await supplierRepo.ExistsAsync(
                x => x.SupplierId != supplierId && x.Name == normalizedName,
                cancellationToken);

            if (duplicate)
            {
                return ServiceResponse<SupplierDto>.Conflict(
                    $"A supplier with name '{normalizedName}' already exists.");
            }

            supplier.Name = normalizedName;
        }

        if (request.Email is not null)
        {
            supplier.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        }

        if (request.Phone is not null)
        {
            supplier.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        }

        if (request.ContactPerson is not null)
        {
            supplier.ContactPerson = string.IsNullOrWhiteSpace(request.ContactPerson) ? null : request.ContactPerson.Trim();
        }

        if (request.LeadTimeDays.HasValue)
        {
            supplier.LeadTimeDays = request.LeadTimeDays.Value;
        }

        if (request.IsActive.HasValue)
        {
            supplier.IsActive = request.IsActive.Value;
        }

        supplier.UpdatedAt = DateTime.UtcNow;
        supplierRepo.Update(supplier);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Supplier, entityId={EntityId}",
            "UpdateSupplier",
            userContext.GetCurrentUser().UserId,
            supplier.SupplierId);

        return ServiceResponse<SupplierDto>.Success(
            mapper.Map<SupplierDto>(supplier),
            "Supplier updated successfully.");
    }

    public async Task<ServiceResponse> SoftDeleteAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var supplierRepo = unitOfWork.Repository<Supplier>();
        var supplier = await supplierRepo.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return ServiceResponse.NotFound("Supplier was not found.");
        }

        supplier.IsDeleted = true;
        supplier.IsActive = false;
        supplier.DeletedAt = DateTime.UtcNow;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplierRepo.Update(supplier);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Supplier, entityId={EntityId}",
            "DeleteSupplier",
            userContext.GetCurrentUser().UserId,
            supplier.SupplierId);

        return ServiceResponse.Success("Supplier deleted successfully.");
    }

    public async Task<ServiceResponse<SupplierDto>> GetByIdAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var supplier = await unitOfWork.Repository<Supplier>().GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return ServiceResponse<SupplierDto>.NotFound("Supplier was not found.");
        }

        return ServiceResponse<SupplierDto>.Success(mapper.Map<SupplierDto>(supplier));
    }

    public async Task<ServiceResponse<PaginationResult<SupplierDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await unitOfWork.Repository<Supplier>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<SupplierDto>
        {
            Records = [.. suppliers.Records.Select(x => mapper.Map<SupplierDto>(x))],
            TotalRecords = suppliers.TotalRecords,
            PageSize = suppliers.PageSize,
            CurrentPage = suppliers.CurrentPage
        };

        return ServiceResponse<PaginationResult<SupplierDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<SupplierDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var suppliers = await unitOfWork.Repository<Supplier>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            x => x.Name.ToLower().Contains(term) ||
                 (x.Email != null && x.Email.ToLower().Contains(term)) ||
                 (x.Phone != null && x.Phone.ToLower().Contains(term)) ||
                 (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(term)),
            cancellationToken);

        var response = new PaginationResult<SupplierDto>
        {
            Records = suppliers.Records.Select(x => mapper.Map<SupplierDto>(x)).ToList(),
            TotalRecords = suppliers.TotalRecords,
            PageSize = suppliers.PageSize,
            CurrentPage = suppliers.CurrentPage
        };

        return ServiceResponse<PaginationResult<SupplierDto>>.Success(response);
    }
}
