using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Responses;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.ProductSuppliers;

public class ProductSupplierService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    ILogger<ProductSupplierService> logger) : IProductSupplierService
{
    public Task<ServiceResponse<ProductSupplierDTO>> CreateAsync(
        Guid productId,
        CreateProductSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        return transactionRunner.ExecuteSerializableAsync("productSupplier.create", async token =>
        {
            var (validationError, supplier) = await ValidateReferencesAsync(productId, request.SupplierId, token);
            if (validationError is not null)
            {
                return validationError;
            }

            var repo = unitOfWork.Repository<ProductSupplier>();
            var existing = await repo.FindAsync(
                x => x.ProductId == productId && x.SupplierId == request.SupplierId,
                token);

            if (existing is not null)
            {
                return ServiceResponse<ProductSupplierDTO>.Conflict(
                    "This supplier is already linked to the selected product.");
            }

            if (request.IsDefault)
            {
                await ClearExistingDefaultAsync(productId, repo, token);
            }

            var entity = new ProductSupplier
            {
                ProductId = productId,
                SupplierId = request.SupplierId,
                UnitCost = request.UnitCost,
                IsDefault = request.IsDefault
            };

            await repo.AddAsync(entity, token);

            logger.LogInformation(
                "Inventory audit: operation={Operation}, entity=ProductSupplier, productId={ProductId}, supplierId={SupplierId}, unitCost={UnitCost}, isDefault={IsDefault}",
                "CreateProductSupplier",
                productId,
                request.SupplierId,
                request.UnitCost,
                request.IsDefault);

            return ServiceResponse<ProductSupplierDTO>.Created(
                Map(entity, supplier!),
                "Supplier linked to product successfully.");
        }, cancellationToken);
    }

    public Task<ServiceResponse<ProductSupplierDTO>> UpdateAsync(
        Guid productId,
        Guid supplierId,
        UpdateProductSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        return transactionRunner.ExecuteSerializableAsync("productSupplier.update", async token =>
        {
            var repo = unitOfWork.Repository<ProductSupplier>();
            var entity = await repo.FindAsync(
                x => x.ProductId == productId && x.SupplierId == supplierId,
                token);

            if (entity is null)
            {
                return ServiceResponse<ProductSupplierDTO>.NotFound("Product-supplier link was not found.");
            }

            var supplier = await unitOfWork.Repository<Supplier>().GetByIdAsync(supplierId, token);
            if (supplier is null)
            {
                return ServiceResponse<ProductSupplierDTO>.BadRequest("Supplier was not found.");
            }

            if (request.IsDefault)
            {
                await ClearExistingDefaultAsync(productId, repo, token);
            }

            entity.UnitCost = request.UnitCost;
            entity.IsDefault = request.IsDefault;
            repo.Update(entity);

            logger.LogInformation(
                "Inventory audit: operation={Operation}, entity=ProductSupplier, productId={ProductId}, supplierId={SupplierId}, unitCost={UnitCost}, isDefault={IsDefault}",
                "UpdateProductSupplier",
                productId,
                supplierId,
                request.UnitCost,
                request.IsDefault);

            return ServiceResponse<ProductSupplierDTO>.Success(
                Map(entity, supplier),
                "Product-supplier link updated successfully.");
        }, cancellationToken);
    }

    public Task<ServiceResponse> DeleteAsync(
        Guid productId,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        return transactionRunner.ExecuteSerializableAsync("productSupplier.delete", async token =>
        {
            var repo = unitOfWork.Repository<ProductSupplier>();
            var entity = await repo.FindAsync(
                x => x.ProductId == productId && x.SupplierId == supplierId,
                token);

            if (entity is null)
            {
                return ServiceResponse.NotFound("Product-supplier link was not found.");
            }

            repo.Remove(entity);

            logger.LogInformation(
                "Inventory audit: operation={Operation}, entity=ProductSupplier, productId={ProductId}, supplierId={SupplierId}",
                "DeleteProductSupplier",
                productId,
                supplierId);

            return ServiceResponse.Success("Product-supplier link deleted successfully.");
        }, cancellationToken);
    }

    private async Task<(ServiceResponse<ProductSupplierDTO>? Error, Supplier? Supplier)> ValidateReferencesAsync(
        Guid productId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return (ServiceResponse<ProductSupplierDTO>.NotFound("Product was not found."), null);
        }

        var supplier = await unitOfWork.Repository<Supplier>().GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return (ServiceResponse<ProductSupplierDTO>.BadRequest("Supplier was not found."), null);
        }

        return (null, supplier);
    }

    private static ProductSupplierDTO Map(ProductSupplier entity, Supplier supplier)
    {
        return new ProductSupplierDTO
        {
            SupplierId = entity.SupplierId,
            SupplierName = supplier.Name,
            SupplierEmail = supplier.Email,
            UnitCost = entity.UnitCost,
            IsDefault = entity.IsDefault
        };
    }

    private static async Task ClearExistingDefaultAsync(
        Guid productId,
        IBaseRepository<ProductSupplier> repo,
        CancellationToken cancellationToken)
    {
        var existingDefault = await repo.FindAsync(
            x => x.ProductId == productId && x.IsDefault,
            cancellationToken);

        if (existingDefault is null)
        {
            return;
        }

        existingDefault.IsDefault = false;
        repo.Update(existingDefault);
    }
}
