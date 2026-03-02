using System.Linq.Expressions;
using KipInventorySystem.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KipInventorySystem.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Add your DbSets here
    // public DbSet<Product> Products => Set<Product>();
    // public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global query filter for soft delete and active status
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Check if entity inherits from BaseEntity
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");

                // Create expression: !e.IsDeleted && e.IsActive
                var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var isActiveProperty = Expression.Property(parameter, nameof(BaseEntity.IsActive));

                var notDeleted = Expression.Not(isDeletedProperty);
                var isActive = isActiveProperty;

                var combinedFilter = Expression.AndAlso(notDeleted, isActive);
                var lambda = Expression.Lambda(combinedFilter, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
