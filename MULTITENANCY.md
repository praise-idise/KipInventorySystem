# Multitenancy Support

## Current State
✅ Template has solid foundation for multitenancy  
❌ Currently single-tenant

## Architecture Options

### 1. Database Per Tenant
- **Pros**: Complete isolation, easy scaling, compliance-friendly
- **Cons**: High cost, complex migrations
- **Complexity**: Medium

### 2. Schema Per Tenant  
- **Pros**: Good isolation, lower cost than separate DBs
- **Cons**: Complex connection management
- **Complexity**: Medium

### 3. Shared Database (Row-Level) ⭐ **Recommended**
- **Pros**: Easy implementation, cost-effective, simple migrations
- **Cons**: Risk of data leakage, less isolation
- **Complexity**: Easy

---

## Implementation (Row-Level)

### 1. Add Tenant Entity
```csharp
public class Tenant : BaseEntity
{
    [Key]
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}
```

### 2. Update BaseEntity
```csharp
public class BaseEntity
{
    public Guid? TenantId { get; set; }  // Add this
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // ... rest
}
```

### 3. Create Tenant Context
```csharp
public interface ITenantContext
{
    Guid? GetTenantId();
}

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Guid? GetTenantId()
    {
        var claim = _httpContextAccessor.HttpContext?.User
            .FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
```

### 4. Add to Global Query Filter
```csharp
// In ApplicationDbContext.OnModelCreating
var tenantIdProperty = Expression.Property(parameter, nameof(BaseEntity.TenantId));
var currentTenantId = Expression.Constant(_tenantContext.GetTenantId(), typeof(Guid?));
var tenantFilter = Expression.Equal(tenantIdProperty, currentTenantId);

// Combine with existing filters
var combined = Expression.AndAlso(existingFilters, tenantFilter);
```

### 5. Add Tenant to JWT
```csharp
var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, user.Id),
    new("tenant_id", user.TenantId.ToString()),  // Add this
};
```

### 6. Tenant Resolution

**From Subdomain:**
```csharp
var subdomain = Request.Host.Host.Split('.')[0];
var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Domain == subdomain);
```

**From Header:**
```csharp
var tenantId = Request.Headers["X-Tenant-Id"];
```

**From JWT:** (Most common)
```csharp
var tenantId = User.FindFirst("tenant_id")?.Value;
```

---

## Security Rules

⚠️ **Critical:**
1. Always validate tenant access
2. Include TenantId in ALL writes
3. Never allow `IgnoreQueryFilters()` for tenant
4. Test query filter thoroughly

### Safe Repository Pattern
```csharp
// ✅ SAFE - Re-apply tenant filter manually
public async Task<List<T>> GetAllIncludingDeletedAsync()
{
    return await _dbSet
        .IgnoreQueryFilters()
        .Where(e => e.TenantId == _tenantContext.GetTenantId())
        .ToListAsync();
}
```

---

## Migration Path

1. **Phase 1**: Add nullable `TenantId`, create Tenant table
2. **Phase 2**: Test with one tenant
3. **Phase 3**: Enable tenant filter, make TenantId required

---

## When to Use

**Use Row-Level Multitenancy for:**
- B2B SaaS applications
- Many small-medium tenants
- Cost-effective scaling

**Use Database-Per-Tenant for:**
- Few large enterprise clients
- Strict data isolation requirements
- Custom schemas per tenant
