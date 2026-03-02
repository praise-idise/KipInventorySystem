# Global Query Filters

## ✅ Already Implemented

All entities inheriting from `BaseEntity` automatically filter:
- `IsDeleted = false`
- `IsActive = true`

## Usage

### Normal Queries (Auto-filtered)
```csharp
var products = await _unitOfWork.Repository<Product>().GetAllAsync();
// Returns: IsDeleted = false AND IsActive = true
```

### Include Deleted/Inactive
```csharp
// Get all (including deleted/inactive)
var all = await _unitOfWork.Repository<Product>().GetAllIncludingDeletedAsync();

// Get by ID (including deleted/inactive)
var product = await _unitOfWork.Repository<Product>().GetByIdIncludingDeletedAsync(id);

// Query with filter (including deleted/inactive)
var inactive = await _unitOfWork.Repository<Product>()
    .WhereIncludingDeletedAsync(p => !p.IsActive);
```

### Raw DbContext
```csharp
var all = await _context.Products.IgnoreQueryFilters().ToListAsync();
```

## Soft Delete Example
```csharp
public async Task DeleteProductAsync(Guid productId)
{
    var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId);
    if (product != null)
    {
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        _unitOfWork.Repository<Product>().Update(product);
        await _unitOfWork.SaveChangesAsync();
    }
}
```

## Modify Filter (ApplicationDbContext)

**Filter only `IsDeleted`:**
```csharp
var notDeleted = Expression.Not(isDeletedProperty);
var lambda = Expression.Lambda(notDeleted, parameter);
```

**Filter `IsDeleted` AND `IsActive`:**
```csharp
var notDeleted = Expression.Not(isDeletedProperty);
var isActive = isActiveProperty;
var combined = Expression.AndAlso(notDeleted, isActive);
var lambda = Expression.Lambda(combined, parameter);
```
