using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using KipInventorySystem.Domain.Entities;
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
        return idempotencyService.ExecuteAsync<CreateProductDTO, ProductDTO>(
            "product-create",
            idempotencyKey,
            request,
            token => transactionRunner.ExecuteSerializableAsync("product.create", async _ =>
            {
                var productRepo = unitOfWork.Repository<Product>();
                var product = mapper.Map<Product>(request);
                NormalizeScalarFields(product);
                product.VariantAttributes = [.. NormalizeVariantAttributes(product.VariantAttributes)];

                if (await ExistsBusinessDuplicateAsync(productRepo, product, token))
                {
                    return ServiceResponse<ProductDTO>.Conflict(
                        "A product with the same category, brand, name, unit of measure, and variant attributes already exists.");
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
            query => query
                .Include(x => x.VariantAttributes)
                .Include(x => x.ProductSuppliers),
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

        if (request.BrandCode is not null)
        {
            product.BrandCode = request.BrandCode.Trim().ToUpperInvariant();
            shouldRegenerateSku = true;
        }

        if (request.VariantAttributes is not null)
        {
            product.VariantAttributes.Clear();

            var variantAttributes = mapper.Map<List<ProductVariantAttribute>>(request.VariantAttributes);
            foreach (var variantAttribute in NormalizeVariantAttributes(variantAttributes))
            {
                product.VariantAttributes.Add(variantAttribute);
            }

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
            product.UnitOfMeasure = request.UnitOfMeasure.Trim();
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

        if (shouldRegenerateSku)
        {
            product.Sku = await GenerateNextSkuAsync(productRepo, product, cancellationToken, product.ProductId);
        }

        product.UpdatedAt = DateTime.UtcNow;

        productRepo.Update(product);
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
        productRepo.Update(product);
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
                .Include(x => x.VariantAttributes)
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
        var products = await unitOfWork.Repository<Product>().GetPagedProjectionAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            BuildSummaryProjection(),
            x => EF.Functions.ILike(x.Name, pattern) ||
                 EF.Functions.ILike(x.Sku, pattern) ||
                 EF.Functions.ILike(x.ItemCode, pattern) ||
                 EF.Functions.ILike(x.BrandCode, pattern) ||
                 EF.Functions.ILike(x.CategoryCode, pattern) ||
                 EF.Functions.ILike(x.UnitOfMeasure, pattern) ||
                 x.VariantAttributes.Any(attribute =>
                     EF.Functions.ILike(attribute.AttributeName, pattern) ||
                     EF.Functions.ILike(attribute.AttributeCode, pattern)),
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
                 x.BrandCode == normalizedProduct.BrandCode &&
                 x.Name == normalizedProduct.Name &&
                 x.UnitOfMeasure == normalizedProduct.UnitOfMeasure,
            cancellationToken);

        foreach (var candidate in candidates)
        {
            if (productIdToIgnore.HasValue && candidate.ProductId == productIdToIgnore.Value)
            {
                continue;
            }

            var existing = await productRepo.GetByIdAsync(
                candidate.ProductId,
                query => query.Include(x => x.VariantAttributes),
                cancellationToken);

            if (existing is null)
            {
                continue;
            }

            var normalizedExistingAttributes = NormalizeVariantAttributes(existing.VariantAttributes);
            if (VariantAttributesMatch(normalizedProduct.VariantAttributes, normalizedExistingAttributes))
            {
                return true;
            }
        }

        return false;
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

        var variantSegments = product.VariantAttributes
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AttributeName)
            .Select(x => NormalizeCodeSegment(x.AttributeCode, 30))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (variantSegments.Count == 0)
        {
            segments.Add("STD");
        }
        else
        {
            segments.AddRange(variantSegments);
        }

        segments.Add(NormalizeCodeSegment(product.UnitOfMeasure, 10));
        return string.Join("-", segments);
    }

    private static void NormalizeScalarFields(Product product)
    {
        product.CategoryCode = NormalizeCodeSegment(product.CategoryCode, 3, padToLength: true);
        product.BrandCode = NormalizeCodeSegment(product.BrandCode, 3, padToLength: true);
        product.Name = product.Name.Trim();
        product.ItemCode = GenerateItemCode(product.Name);
        product.Description = string.IsNullOrWhiteSpace(product.Description) ? null : product.Description.Trim();
        product.UnitOfMeasure = product.UnitOfMeasure.Trim();
    }

    private static List<ProductVariantAttribute> NormalizeVariantAttributes(IEnumerable<ProductVariantAttribute> attributes)
    {
        return [.. attributes
            .Select(attribute => new ProductVariantAttribute
            {
                ProductVariantAttributeId = attribute.ProductVariantAttributeId == Guid.Empty
                    ? Guid.CreateVersion7()
                    : attribute.ProductVariantAttributeId,
                ProductId = attribute.ProductId,
                AttributeName = string.IsNullOrWhiteSpace(attribute.AttributeName)
                    ? string.Empty
                    : attribute.AttributeName.Trim(),
                AttributeCode = NormalizeCodeSegment(attribute.AttributeCode, 30),
                SortOrder = attribute.SortOrder
            })
            .OrderBy(attribute => attribute.SortOrder)
            .ThenBy(attribute => attribute.AttributeName)];
    }

    private static bool VariantAttributesMatch(
        IEnumerable<ProductVariantAttribute> left,
        IEnumerable<ProductVariantAttribute> right)
    {
        var leftList = left.ToList();
        var rightList = right.ToList();

        if (leftList.Count != rightList.Count)
        {
            return false;
        }

        for (var i = 0; i < leftList.Count; i++)
        {
            if (!string.Equals(leftList[i].AttributeName, rightList[i].AttributeName, StringComparison.Ordinal) ||
                !string.Equals(leftList[i].AttributeCode, rightList[i].AttributeCode, StringComparison.Ordinal) ||
                leftList[i].SortOrder != rightList[i].SortOrder)
            {
                return false;
            }
        }

        return true;
    }

    private static System.Linq.Expressions.Expression<Func<Product, ProductDTO>> BuildSummaryProjection()
    {
        return product => new ProductDTO
        {
            ProductId = product.ProductId,
            Sku = product.Sku,
            CategoryCode = product.CategoryCode,
            BrandCode = product.BrandCode,
            ItemCode = product.ItemCode,
            Name = product.Name,
            Description = product.Description,
            UnitOfMeasure = product.UnitOfMeasure,
            VariantAttributes = product.VariantAttributes
                .OrderBy(attribute => attribute.SortOrder)
                .Select(attribute => new ProductVariantAttributeDTO
                {
                    AttributeName = attribute.AttributeName,
                    AttributeCode = attribute.AttributeCode,
                    SortOrder = attribute.SortOrder
                })
                .ToList(),
            Suppliers = product.ProductSuppliers
                .OrderByDescending(supplier => supplier.IsDefault)
                .ThenBy(supplier => supplier.SupplierId)
                .Select(supplier => new ProductSupplierDTO
                {
                    SupplierName = supplier.Supplier.Name,
                })
                .ToList(),
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
