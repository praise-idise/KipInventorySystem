# Global Query Filters

## Already Implemented

All entities inheriting from `BaseEntity` automatically filter:
- `IsDeleted = false`

## Usage

### Normal Queries (Auto-filtered)
```csharp
var products = await _unitOfWork.Repository<Product>().GetAllAsync();
// Returns only records where IsDeleted = false
```

### Include Deleted
```csharp
// Get all (including deleted)
var all = await _unitOfWork.Repository<Product>().GetAllIncludingDeletedAsync();

// Get by ID (including deleted)
var product = await _unitOfWork.Repository<Product>().GetByIdIncludingDeletedAsync(id);

// Query with filter (including deleted)
var deleted = await _unitOfWork.Repository<Product>()
    .WhereIncludingDeletedAsync(p => p.IsDeleted);
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

## Current Filter (ApplicationDbContext)
```csharp
var notDeleted = Expression.Not(isDeletedProperty);
var lambda = Expression.Lambda(notDeleted, parameter);
```
