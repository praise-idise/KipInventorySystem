using System.Linq.Expressions;
using KipInventorySystem.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KipInventorySystem.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseCodeCounter> WarehouseCodeCounters => Set<WarehouseCodeCounter>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariantAttribute> ProductVariantAttributes => Set<ProductVariantAttribute>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();
    public DbSet<TransferRequestLine> TransferRequestLines => Set<TransferRequestLine>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockAdjustmentLine> StockAdjustmentLines => Set<StockAdjustmentLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("citext");
        ConfigureInventoryModel(modelBuilder);

        // Apply global query filter for soft delete.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Check if entity inherits from BaseEntity
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");

                // Create expression: !e.IsDeleted
                var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var notDeleted = Expression.Not(isDeletedProperty);
                var lambda = Expression.Lambda(notDeleted, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    private static void ConfigureInventoryModel(ModelBuilder modelBuilder)
    {
        // Business invariants that EF can't infer.
        modelBuilder.Entity<Warehouse>()
            .HasIndex(e => e.Code)
            .IsUnique();

        modelBuilder.Entity<Warehouse>()
            .Property(e => e.Name)
            .HasColumnType("citext");

        modelBuilder.Entity<Warehouse>()
            .HasIndex(e => e.Name)
            .HasFilter("\"IsDeleted\" = false")
            .IsUnique();

        modelBuilder.Entity<WarehouseCodeCounter>()
            .Property(e => e.StateCode)
            .HasMaxLength(3);

        modelBuilder.Entity<Product>()
            .HasIndex(e => e.Sku)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasMany(e => e.VariantAttributes)
            .WithOne(e => e.Product)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductVariantAttribute>()
            .HasIndex(e => new { e.ProductId, e.AttributeName })
            .IsUnique();

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(e => e.PurchaseOrderNumber)
            .IsUnique();

        modelBuilder.Entity<WarehouseInventory>()
            .HasIndex(e => new { e.WarehouseId, e.ProductId })
            .IsUnique();

        modelBuilder.Entity<PurchaseOrderLine>()
            .Property(e => e.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<TransferRequest>()
            .HasIndex(e => e.TransferNumber)
            .IsUnique();

        modelBuilder.Entity<StockAdjustment>()
            .HasIndex(e => e.AdjustmentNumber)
            .IsUnique();

        modelBuilder.Entity<SalesOrder>()
            .HasIndex(e => e.SalesOrderNumber)
            .IsUnique();

        modelBuilder.Entity<TransferRequestLine>()
            .HasIndex(e => new { e.TransferRequestId, e.ProductId })
            .IsUnique();

        modelBuilder.Entity<SalesOrderLine>()
            .HasIndex(e => new { e.SalesOrderId, e.ProductId })
            .IsUnique();

        modelBuilder.Entity<SalesOrderLine>()
            .Property(e => e.UnitPrice)
            .HasPrecision(18, 2);

        // Prevent accidental cascading deletes in inventory domain.
        var inventoryEntityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => e.ClrType is not null && typeof(BaseEntity).IsAssignableFrom(e.ClrType));

        foreach (var entityType in inventoryEntityTypes)
        {
            foreach (var foreignKey in entityType.GetForeignKeys().Where(fk => !fk.IsOwnership))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
