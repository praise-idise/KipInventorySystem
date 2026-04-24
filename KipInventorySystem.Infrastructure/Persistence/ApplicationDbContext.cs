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
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();
    public DbSet<TransferRequestLine> TransferRequestLines => Set<TransferRequestLine>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockAdjustmentLine> StockAdjustmentLines => Set<StockAdjustmentLine>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();
    public DbSet<OpeningBalanceLine> OpeningBalanceLines => Set<OpeningBalanceLine>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<Customer> Customers => Set<Customer>();
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

        modelBuilder.Entity<Supplier>()
            .Property(e => e.Name)
            .HasColumnType("citext");

        modelBuilder.Entity<Supplier>()
            .Property(e => e.Email)
            .HasColumnType("citext");

        modelBuilder.Entity<Supplier>()
            .HasIndex(e => e.Name)
            .IsUnique();

        modelBuilder.Entity<Supplier>()
            .HasIndex(e => e.Email)
            .HasFilter("\"Email\" IS NOT NULL")
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(e => e.Name)
            .HasColumnType("citext");

        modelBuilder.Entity<Product>()
            .Property(e => e.UnitOfMeasure)
            .HasColumnType("citext");

        modelBuilder.Entity<Product>()
            .HasIndex(e => e.Sku)
            .IsUnique();

        modelBuilder.Entity<ProductSupplier>()
            .HasKey(e => new { e.ProductId, e.SupplierId });

        modelBuilder.Entity<ProductSupplier>()
            .HasOne(e => e.Product)
            .WithMany(e => e.ProductSuppliers)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductSupplier>()
            .HasOne(e => e.Supplier)
            .WithMany(e => e.ProductSuppliers)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductSupplier>()
            .HasQueryFilter(e => !e.Product.IsDeleted && !e.Supplier.IsDeleted);

        modelBuilder.Entity<ProductSupplier>()
            .HasIndex(e => e.ProductId)
            .HasFilter("\"IsDefault\" = true")
            .IsUnique();

        modelBuilder.Entity<ProductSupplier>()
            .Property(e => e.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(e => e.PurchaseOrderNumber)
            .IsUnique();

        modelBuilder.Entity<WarehouseInventory>()
            .HasIndex(e => new { e.WarehouseId, e.ProductId })
            .IsUnique();

        modelBuilder.Entity<WarehouseInventory>()
            .Property(e => e.AverageUnitCost)
            .HasPrecision(18, 4);

        modelBuilder.Entity<WarehouseInventory>()
            .Property(e => e.InventoryValue)
            .HasPrecision(18, 4);

        modelBuilder.Entity<PurchaseOrderLine>()
            .Property(e => e.UnitCost)
            .HasPrecision(18, 2);

        modelBuilder.Entity<StockMovement>()
            .Property(e => e.UnitCost)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockMovement>()
            .Property(e => e.TotalCost)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockAdjustmentLine>()
            .Property(e => e.UnitCost)
            .HasPrecision(18, 4);

        modelBuilder.Entity<OpeningBalance>()
            .HasIndex(e => e.OpeningBalanceNumber)
            .IsUnique();

        modelBuilder.Entity<OpeningBalanceLine>()
            .HasIndex(e => new { e.OpeningBalanceId, e.ProductId })
            .IsUnique();

        modelBuilder.Entity<OpeningBalanceLine>()
            .Property(e => e.UnitCost)
            .HasPrecision(18, 4);

        modelBuilder.Entity<OpeningBalanceLine>()
            .Property(e => e.TotalCost)
            .HasPrecision(18, 4);

        modelBuilder.Entity<TransferRequest>()
            .HasIndex(e => e.TransferNumber)
            .IsUnique();

        modelBuilder.Entity<StockAdjustment>()
            .HasIndex(e => e.AdjustmentNumber)
            .IsUnique();

        modelBuilder.Entity<ApprovalRequest>()
            .HasIndex(e => new { e.DocumentType, e.DocumentId, e.RequestedAt });

        modelBuilder.Entity<ApprovalRequest>()
            .Property(e => e.Comment)
            .HasMaxLength(1000);

        modelBuilder.Entity<Customer>()
            .Property(e => e.Name)
            .HasColumnType("citext");

        modelBuilder.Entity<Customer>()
            .Property(e => e.Email)
            .HasColumnType("citext");

        modelBuilder.Entity<Customer>()
            .HasIndex(e => e.Email)
            .HasFilter("\"Email\" IS NOT NULL")
            .IsUnique();

        modelBuilder.Entity<SalesOrder>()
            .HasIndex(e => e.SalesOrderNumber)
            .IsUnique();

        modelBuilder.Entity<SalesOrder>()
            .HasOne(e => e.Customer)
            .WithMany(e => e.SalesOrders)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

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
