using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace KipInventorySystem.Application.Services.Inventory.Products;

public partial class ProductService(
    IUnitOfWork unitOfWork,
    IInventoryTransactionRunner transactionRunner,
    IIdempotencyService idempotencyService,
    IUserContext userContext,
    IMapper mapper,
    ILogger<ProductService> logger) : IProductService
{
    private static readonly HashSet<string> IgnoredItemCodeTokens =
    [
        "THE",
        "AND",
        "FOR",
        "WITH",
        "OF",
        "PCS",
        "PIECE",
        "PIECES",
        "BOX",
        "BOTTLE",
        "CARTON",
        "PACK"
    ];

    public Task<ServiceResponse<ProductDTO>> CreateAsync(
        CreateProductDTO request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "product-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("product.create", async _ =>
            {
                var productRepo = unitOfWork.Repository<Product>();
                var product = mapper.Map<Product>(request);
                product.Brand = request.Brand.Trim();
                product.BrandCode = GenerateBrandCode(request.Brand);

                NormalizeScalarFields(product);

                if (await ExistsBusinessDuplicateAsync(productRepo, product, token))
                {
                    return ServiceResponse<ProductDTO>.Conflict(
                        "A product with the same category, brand, name, unit of measure, and variant values already exists.");
                }

                product.Sku = await GenerateNextSkuAsync(productRepo, product, token);
                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;

                await productRepo.AddAsync(product, token);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=Product, entityId={EntityId}, sku={Sku}",
                    "CreateProduct",
                    userContext.GetCurrentUser().UserId,
                    product.ProductId,
                    product.Sku);

                return ServiceResponse<ProductDTO>.Created(
                    mapper.Map<ProductDTO>(product),
                    "Product created successfully.");
            }, token),
            cancellationToken);
    }

    public async Task<ServiceResponse<ProductDTO>> UpdateAsync(
        Guid productId,
        UpdateProductDTO request,
        CancellationToken cancellationToken = default)
    {
        var productRepo = unitOfWork.Repository<Product>();
        var product = await productRepo.GetByIdAsync(
            productId,
            query => query.Include(x => x.ProductSuppliers),
            cancellationToken);
        if (product is null)
        {
            return ServiceResponse<ProductDTO>.NotFound("Product was not found.");
        }

        var shouldRegenerateSku = false;

        if (request.CategoryCode is not null)
        {
            product.CategoryCode = request.CategoryCode.Trim().ToUpperInvariant();
            shouldRegenerateSku = true;
        }

        if (request.Brand is not null)
        {
            product.Brand = request.Brand.Trim();
            product.BrandCode = GenerateBrandCode(request.Brand);
            shouldRegenerateSku = true;
        }

        if (request.Name is not null)
        {
            product.Name = request.Name.Trim();
            shouldRegenerateSku = true;
        }

        if (request.Description is not null)
        {
            product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.UnitOfMeasure is not null)
        {
            product.UnitOfMeasure = request.UnitOfMeasure.Value;
            shouldRegenerateSku = true;
        }

        if (request.Color is not null)
        {
            product.Color = request.Color;
            shouldRegenerateSku = true;
        }

        if (request.Storage is not null)
        {
            product.Storage = request.Storage;
            shouldRegenerateSku = true;
        }

        if (request.Size is not null)
        {
            product.Size = request.Size;
            shouldRegenerateSku = true;
        }

        if (request.Dosage is not null)
        {
            product.Dosage = request.Dosage;
            shouldRegenerateSku = true;
        }

        if (request.Grade is not null)
        {
            product.Grade = request.Grade;
            shouldRegenerateSku = true;
        }

        if (request.Finish is not null)
        {
            product.Finish = request.Finish;
            shouldRegenerateSku = true;
        }

        if (request.ReorderThreshold.HasValue)
        {
            product.ReorderThreshold = request.ReorderThreshold.Value;
        }

        if (request.ReorderQuantity.HasValue)
        {
            product.ReorderQuantity = request.ReorderQuantity.Value;
        }

        if (request.IsActive.HasValue)
        {
            product.IsActive = request.IsActive.Value;
        }

        NormalizeScalarFields(product);

        if (await ExistsBusinessDuplicateAsync(productRepo, product, cancellationToken, product.ProductId))
        {
            return ServiceResponse<ProductDTO>.Conflict(
                "A product with the same category, brand, name, unit of measure, and variant values already exists.");
        }

        // If SKU-related fields were updated, we need to check if the new SKU would conflict with existing products and regenerate it if necessary
        if (shouldRegenerateSku)
        {
            product.Sku = await GenerateNextSkuAsync(productRepo, product, cancellationToken, product.ProductId);
        }

        product.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Product, entityId={EntityId}",
            "UpdateProduct",
            userContext.GetCurrentUser().UserId,
            product.ProductId);

        return ServiceResponse<ProductDTO>.Success(
            mapper.Map<ProductDTO>(product),
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Product, entityId={EntityId}",
            "DeleteProduct",
            userContext.GetCurrentUser().UserId,
            product.ProductId);

        return ServiceResponse.Success("Product deleted successfully.");
    }

    public async Task<ServiceResponse<ProductDTO>> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(
            productId,
            query => query
                .Include(x => x.ProductSuppliers)
                    .ThenInclude(x => x.Supplier),
            cancellationToken);
        if (product is null)
        {
            return ServiceResponse<ProductDTO>.NotFound("Product was not found.");
        }

        return ServiceResponse<ProductDTO>.Success(mapper.Map<ProductDTO>(product));
    }

    public async Task<ServiceResponse<PaginationResult<ProductDTO>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var products = await unitOfWork.Repository<Product>().GetPagedProjectionAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            BuildSummaryProjection(),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<ProductDTO>
        {
            Records = products.Records,
            TotalRecords = products.TotalRecords,
            PageSize = products.PageSize,
            CurrentPage = products.CurrentPage
        };

        return ServiceResponse<PaginationResult<ProductDTO>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<ProductDTO>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var pattern = $"%{searchTerm.Trim()}%";
        var hasUnitOfMeasure = Enum.TryParse<UnitOfMeasure>(searchTerm.Trim(), true, out var parsedUnitOfMeasure);
        var hasSize = Enum.TryParse<ProductSize>(searchTerm.Trim(), true, out var parsedSize);
        var products = await unitOfWork.Repository<Product>().GetPagedProjectionAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            BuildSummaryProjection(),
            x => EF.Functions.ILike(x.Name, pattern) ||
                 EF.Functions.ILike(x.Sku, pattern) ||
                 EF.Functions.ILike(x.ItemCode, pattern) ||
                  EF.Functions.ILike(x.Brand, pattern) ||
                 EF.Functions.ILike(x.BrandCode, pattern) ||
                 EF.Functions.ILike(x.CategoryCode, pattern) ||
              (hasUnitOfMeasure && x.UnitOfMeasure == parsedUnitOfMeasure) ||
                 (x.Color != null && EF.Functions.ILike(x.Color, pattern)) ||
                 (x.Storage != null && EF.Functions.ILike(x.Storage, pattern)) ||
              (hasSize && x.Size == parsedSize) ||
                 (x.Dosage != null && EF.Functions.ILike(x.Dosage, pattern)) ||
                 (x.Grade != null && EF.Functions.ILike(x.Grade, pattern)) ||
                 (x.Finish != null && EF.Functions.ILike(x.Finish, pattern)),
            cancellationToken);

        var response = new PaginationResult<ProductDTO>
        {
            Records = products.Records,
            TotalRecords = products.TotalRecords,
            PageSize = products.PageSize,
            CurrentPage = products.CurrentPage
        };

        return ServiceResponse<PaginationResult<ProductDTO>>.Success(response);
    }

    private static async Task<string> GenerateNextSkuAsync(
        IBaseRepository<Product> productRepo,
        Product product,
        CancellationToken cancellationToken,
        Guid? productIdToIgnore = null)
    {
        var baseSku = BuildBaseSku(product);

        var matching = await productRepo.WhereIncludingDeletedAsync(
            x => x.Sku == baseSku || x.Sku.StartsWith($"{baseSku}-"),
            cancellationToken);

        var maxSequence = matching
            .Where(x => !productIdToIgnore.HasValue || x.ProductId != productIdToIgnore.Value)
            .Select(x => ParseSkuSequence(x.Sku, baseSku))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return maxSequence == 0 ? baseSku : $"{baseSku}-{maxSequence + 1:00}";
    }

    private static async Task<bool> ExistsBusinessDuplicateAsync(
        IBaseRepository<Product> productRepo,
        Product normalizedProduct,
        CancellationToken cancellationToken,
        Guid? productIdToIgnore = null)
    {
        var candidates = await productRepo.WhereAsync(
            x => x.CategoryCode == normalizedProduct.CategoryCode &&
                 x.Brand == normalizedProduct.Brand &&
                 x.Name == normalizedProduct.Name &&
                 x.UnitOfMeasure == normalizedProduct.UnitOfMeasure &&
                 x.Color == normalizedProduct.Color &&
                 x.Storage == normalizedProduct.Storage &&
                 x.Size == normalizedProduct.Size &&
                 x.Dosage == normalizedProduct.Dosage &&
                 x.Grade == normalizedProduct.Grade &&
                 x.Finish == normalizedProduct.Finish,
            cancellationToken);

        return candidates.Any(candidate => !productIdToIgnore.HasValue || candidate.ProductId != productIdToIgnore.Value);
    }

    private static int? ParseSkuSequence(string sku, string baseSku)
    {
        if (string.Equals(sku, baseSku, StringComparison.Ordinal))
        {
            return 1;
        }

        if (!sku.StartsWith($"{baseSku}-", StringComparison.Ordinal))
        {
            return null;
        }

        var suffix = sku[(baseSku.Length + 1)..];
        return int.TryParse(suffix, out var value) ? value : null;
    }

    private static string BuildBaseSku(Product product)
    {
        var segments = new List<string>
        {
            NormalizeCodeSegment(product.CategoryCode, 3, padToLength: true),
            NormalizeCodeSegment(product.BrandCode, 3, padToLength: true),
            NormalizeCodeSegment(product.ItemCode, 20)
        };

        var variantSegments = new[]
        {
            product.Color,
            product.Storage,
            product.Size?.ToString(),
            product.Dosage,
            product.Grade,
            product.Finish
        }
            .Select(value => NormalizeCodeSegment(value ?? string.Empty, 30))
            .Where(value => !string.Equals(value, "STD", StringComparison.Ordinal))
            .ToList();

        if (variantSegments.Count == 0)
        {
            segments.Add("STD");
        }
        else
        {
            segments.AddRange(variantSegments);
        }

        segments.Add(NormalizeCodeSegment(product.UnitOfMeasure.ToString(), 10));
        return string.Join("-", segments);
    }

    private static void NormalizeScalarFields(Product product)
    {
        product.CategoryCode = NormalizeCodeSegment(product.CategoryCode, 3, padToLength: true);
        product.Brand = product.Brand.Trim();
        product.BrandCode = NormalizeCodeSegment(product.BrandCode, 3, padToLength: true);
        product.Name = product.Name.Trim();
        product.ItemCode = GenerateItemCode(product.Name);
        product.Description = string.IsNullOrWhiteSpace(product.Description) ? null : product.Description.Trim();
        product.Color = NormalizeOptionalVariantValue(product.Color);
        product.Storage = NormalizeOptionalVariantValue(product.Storage);
        product.Dosage = NormalizeOptionalVariantValue(product.Dosage);
        product.Grade = NormalizeOptionalVariantValue(product.Grade);
        product.Finish = NormalizeOptionalVariantValue(product.Finish);
    }

    private static System.Linq.Expressions.Expression<Func<Product, ProductDTO>> BuildSummaryProjection()
    {
        return product => new ProductDTO
        {
            ProductId = product.ProductId,
            Sku = product.Sku,
            CategoryCode = product.CategoryCode,
            BrandCode = product.BrandCode,
            Brand = product.Brand,
            ItemCode = product.ItemCode,
            Name = product.Name,
            Description = product.Description,
            UnitOfMeasure = product.UnitOfMeasure,
            Color = product.Color,
            Storage = product.Storage,
            Size = product.Size,
            Dosage = product.Dosage,
            Grade = product.Grade,
            Finish = product.Finish,
            Suppliers = null,
            ReorderThreshold = product.ReorderThreshold,
            ReorderQuantity = product.ReorderQuantity,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private static string NormalizeCodeSegment(string value, int maxLength, bool padToLength = false)
    {
        var cleaned = NormalizeAlphanumeric(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return padToLength ? new string('X', maxLength) : "STD";
        }

        if (cleaned.Length > maxLength)
        {
            return cleaned[..maxLength];
        }

        return padToLength ? cleaned.PadRight(maxLength, 'X') : cleaned;
    }

    private static string NormalizeAlphanumeric(string value)
        => new string([.. value.Where(char.IsLetterOrDigit)]).ToUpperInvariant();

    private static string GenerateBrandCode(string? brand)
    {
        var normalized = new string([.. (brand ?? string.Empty)
            .Where(char.IsLetterOrDigit)])
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "XXX";
        }

        return normalized.Length >= 3
            ? normalized[..3]
            : normalized.PadRight(3, 'X');
    }

    private static string? NormalizeOptionalVariantValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string GenerateItemCode(string productName)
    {
        var tokens = MyRegex().Matches(productName)
            .Select(match => NormalizeAlphanumeric(match.Value))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Where(token => !IgnoredItemCodeTokens.Contains(token))
            .ToList();

        if (tokens.Count == 0)
        {
            return "STD";
        }

        const int maxLength = 20;
        var parts = new List<string>();
        var currentLength = 0;

        foreach (var token in tokens)
        {
            var segment = BuildItemCodeSegment(token);
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var remaining = maxLength - currentLength;
            if (remaining <= 0)
            {
                break;
            }

            if (segment.Length > remaining)
            {
                segment = segment[..remaining];
            }

            parts.Add(segment);
            currentLength += segment.Length;
        }

        var itemCode = string.Concat(parts);
        return string.IsNullOrWhiteSpace(itemCode) ? "STD" : itemCode;
    }

    private static string BuildItemCodeSegment(string token)
    {
        if (token.Any(char.IsDigit))
        {
            return token.Length > 4 ? token[..4] : token;
        }

        if (token.Length <= 3)
        {
            return token;
        }

        var first = token[0];
        var consonants = token[1..]
            .Where(char.IsLetter)
            .Where(character => !"AEIOU".Contains(character))
            .ToList();

        if (consonants.Count >= 2)
        {
            return string.Concat(first, consonants[0], consonants[1]);
        }

        return token[..Math.Min(3, token.Length)];
    }

    [GeneratedRegex("[A-Za-z0-9]+")]
    private static partial Regex MyRegex();
}
