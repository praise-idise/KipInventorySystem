using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Products;

public class ProductService(
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IMapper mapper,
    ILogger<ProductService> logger) : IProductService
{
    private const int MaxCreateAttempts = 5;

    public async Task<ServiceResponse<ProductDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var productRepo = unitOfWork.Repository<Product>();
        var supplierRepo = unitOfWork.Repository<Supplier>();

        if (request.DefaultSupplierId.HasValue)
        {
            var supplier = await supplierRepo.GetByIdAsync(request.DefaultSupplierId.Value, cancellationToken);
            if (supplier is null)
            {
                return ServiceResponse<ProductDto>.BadRequest("Default supplier was not found.");
            }
        }

        for (var attempt = 1; attempt <= MaxCreateAttempts; attempt++)
        {
            var product = mapper.Map<Product>(request);
            product.Sku = await GenerateNextSkuAsync(productRepo, request, cancellationToken);
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;

            try
            {
                await productRepo.AddAsync(product, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=Product, entityId={EntityId}, sku={Sku}",
                    "CreateProduct",
                    userContext.GetCurrentUser().UserId,
                    product.ProductId,
                    product.Sku);

                return ServiceResponse<ProductDto>.Created(
                    mapper.Map<ProductDto>(product),
                    "Product created successfully.");
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxCreateAttempts)
            {
                productRepo.Remove(product);
                logger.LogWarning(
                    ex,
                    "Product SKU generation collision on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    attempt,
                    MaxCreateAttempts);
            }
        }

        return ServiceResponse<ProductDto>.Conflict(
            "Unable to generate a unique SKU. Please retry.");
    }

    public async Task<ServiceResponse<ProductDto>> UpdateAsync(
        Guid productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var productRepo = unitOfWork.Repository<Product>();
        var supplierRepo = unitOfWork.Repository<Supplier>();
        var product = await productRepo.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResponse<ProductDto>.NotFound("Product was not found.");
        }

        if (request.Name is not null)
        {
            product.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.UnitOfMeasure is not null)
        {
            product.UnitOfMeasure = request.UnitOfMeasure.Trim();
        }

        if (request.ReorderThreshold.HasValue)
        {
            product.ReorderThreshold = request.ReorderThreshold.Value;
        }

        if (request.ReorderQuantity.HasValue)
        {
            product.ReorderQuantity = request.ReorderQuantity.Value;
        }

        if (request.DefaultSupplierId.HasValue)
        {
            var supplier = await supplierRepo.GetByIdAsync(request.DefaultSupplierId.Value, cancellationToken);
            if (supplier is null)
            {
                return ServiceResponse<ProductDto>.BadRequest("Default supplier was not found.");
            }

            product.DefaultSupplierId = request.DefaultSupplierId.Value;
        }

        if (request.IsActive.HasValue)
        {
            product.IsActive = request.IsActive.Value;
        }

        product.UpdatedAt = DateTime.UtcNow;

        productRepo.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Product, entityId={EntityId}",
            "UpdateProduct",
            userContext.GetCurrentUser().UserId,
            product.ProductId);

        return ServiceResponse<ProductDto>.Success(
            mapper.Map<ProductDto>(product),
            "Product updated successfully.");
    }

    public async Task<ServiceResponse> SoftDeleteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var productRepo = unitOfWork.Repository<Product>();
        var product = await productRepo.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResponse.NotFound("Product was not found.");
        }

        product.IsDeleted = true;
        product.IsActive = false;
        product.DeletedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        productRepo.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Product, entityId={EntityId}",
            "DeleteProduct",
            userContext.GetCurrentUser().UserId,
            product.ProductId);

        return ServiceResponse.Success("Product deleted successfully.");
    }

    public async Task<ServiceResponse<ProductDto>> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ServiceResponse<ProductDto>.NotFound("Product was not found.");
        }

        return ServiceResponse<ProductDto>.Success(mapper.Map<ProductDto>(product));
    }

    public async Task<ServiceResponse<PaginationResult<ProductDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var products = await unitOfWork.Repository<Product>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<ProductDto>
        {
            Records = products.Records.Select(x => mapper.Map<ProductDto>(x)).ToList(),
            TotalRecords = products.TotalRecords,
            PageSize = products.PageSize,
            CurrentPage = products.CurrentPage
        };

        return ServiceResponse<PaginationResult<ProductDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<ProductDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var term = searchTerm.Trim().ToLower();
        var products = await unitOfWork.Repository<Product>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            x => x.Name.ToLower().Contains(term) || x.Sku.ToLower().Contains(term),
            cancellationToken);

        var response = new PaginationResult<ProductDto>
        {
            Records = [.. products.Records.Select(x => mapper.Map<ProductDto>(x))],
            TotalRecords = products.TotalRecords,
            PageSize = products.PageSize,
            CurrentPage = products.CurrentPage
        };

        return ServiceResponse<PaginationResult<ProductDto>>.Success(response);
    }

    private static async Task<string> GenerateNextSkuAsync(
        IBaseRepository<Product> productRepo,
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var categoryCode = NormalizeThreeCharCode(request.CategoryCode);
        var brandCode = NormalizeThreeCharCode(request.BrandCode);
        var variantCode = NormalizeVariantCode(request.VariantCode);

        var prefix = $"SKU-{categoryCode}-{brandCode}-";
        var suffix = $"-{variantCode}";

        var matching = await productRepo.WhereIncludingDeletedAsync(
            x => x.Sku.StartsWith(prefix) && x.Sku.EndsWith(suffix),
            cancellationToken);

        var maxItem = matching
            .Select(x => ParseItemSequence(x.Sku, prefix, suffix))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxItem + 1:000}{suffix}";
    }

    private static int? ParseItemSequence(string sku, string prefix, string suffix)
    {
        if (!sku.StartsWith(prefix, StringComparison.Ordinal) ||
            !sku.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var itemPartLength = sku.Length - prefix.Length - suffix.Length;
        if (itemPartLength <= 0)
        {
            return null;
        }

        var itemPart = sku.Substring(prefix.Length, itemPartLength);
        return int.TryParse(itemPart, out var value) ? value : null;
    }

    private static string NormalizeThreeCharCode(string value)
    {
        var cleaned = NormalizeAlphanumeric(value);
        if (cleaned.Length >= 3)
        {
            return cleaned[..3];
        }

        return cleaned.PadRight(3, 'X');
    }

    private static string NormalizeVariantCode(string value)
    {
        var cleaned = NormalizeAlphanumeric(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "STD";
        }

        return cleaned.Length > 10 ? cleaned[..10] : cleaned;
    }

    private static string NormalizeAlphanumeric(string value)
        => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

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
