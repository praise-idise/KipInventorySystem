using System.Linq.Expressions;
using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Enums;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KipInventorySystem.Application.Tests.Services.Inventory.Common;

public class LowStockBackgroundJobsTests
{
    [Fact]
    public async Task EvaluateLowStockAsync_WhenStockIsAboveThreshold_DoesNothing()
    {
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var context = LowStockTestContext.Create(
            inventories:
            [
                new WarehouseInventory
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    QuantityOnHand = 30,
                    ReservedQuantity = 5,
                    ReorderThresholdOverride = 10
                }
            ],
            products:
            [
                new Product
                {
                    ProductId = productId,
                    Name = "Rice",
                    Sku = "RICE-001",
                    ReorderThreshold = 10,
                    ReorderQuantity = 12
                }
            ],
            warehouses:
            [
                new Warehouse
                {
                    WarehouseId = warehouseId,
                    Name = "Main Warehouse",
                    Code = "WH-MAIN"
                }
            ]);

        var sut = context.CreateSut();

        await sut.EvaluateLowStockAsync(warehouseId, productId);

        Assert.Empty(context.PurchaseOrders.Items);
        Assert.Empty(context.PurchaseOrderLines.Items);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(0, context.DocumentNumberGenerator.CallCount);
        Assert.Empty(context.EmailBackgroundJobs.LowStockAlerts);
        Assert.Empty(context.EmailBackgroundJobs.ManualReviews);
    }

    [Fact]
    public async Task EvaluateLowStockAsync_WhenProductIsLowStockAndHasDefaultSupplier_CreatesDraftPurchaseOrder()
    {
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var context = LowStockTestContext.Create(
            inventories:
            [
                new WarehouseInventory
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    QuantityOnHand = 12,
                    ReservedQuantity = 4,
                    ReorderThresholdOverride = 8
                }
            ],
            products:
            [
                new Product
                {
                    ProductId = productId,
                    Name = "Beans",
                    Sku = "BEANS-001",
                    ReorderThreshold = 8,
                    ReorderQuantity = 20
                }
            ],
            warehouses:
            [
                new Warehouse
                {
                    WarehouseId = warehouseId,
                    Name = "Main Warehouse",
                    Code = "WH-MAIN"
                }
            ],
            productSuppliers:
            [
                new ProductSupplier
                {
                    ProductId = productId,
                    SupplierId = supplierId,
                    IsDefault = true,
                    UnitCost = 1500m
                }
            ],
            generatedNumbers: ["PO-1001"]);

        var sut = context.CreateSut();

        await sut.EvaluateLowStockAsync(warehouseId, productId);

        var purchaseOrder = Assert.Single(context.PurchaseOrders.Items);
        Assert.Equal("PO-1001", purchaseOrder.PurchaseOrderNumber);
        Assert.Equal(warehouseId, purchaseOrder.WarehouseId);
        Assert.Equal(supplierId, purchaseOrder.SupplierId);
        Assert.Equal(PurchaseOrderStatus.Draft, purchaseOrder.Status);

        var line = Assert.Single(context.PurchaseOrderLines.Items);
        Assert.Equal(purchaseOrder.PurchaseOrderId, line.PurchaseOrderId);
        Assert.Equal(productId, line.ProductId);
        Assert.Equal(20, line.QuantityOrdered);
        Assert.Equal(0, line.QuantityReceived);
        Assert.Equal(1500m, line.UnitCost);

        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(1, context.DocumentNumberGenerator.CallCount);
    }

    [Fact]
    public async Task EvaluateLowStockAsync_WhenOutstandingOpenPurchaseOrderExists_DoesNotCreateDuplicateReorder()
    {
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var existingPurchaseOrderId = Guid.NewGuid();

        var context = LowStockTestContext.Create(
            inventories:
            [
                new WarehouseInventory
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    QuantityOnHand = 10,
                    ReservedQuantity = 3,
                    ReorderThresholdOverride = 7
                }
            ],
            products:
            [
                new Product
                {
                    ProductId = productId,
                    Name = "Milk",
                    Sku = "MILK-001",
                    ReorderThreshold = 7,
                    ReorderQuantity = 14
                }
            ],
            warehouses:
            [
                new Warehouse
                {
                    WarehouseId = warehouseId,
                    Name = "Main Warehouse",
                    Code = "WH-MAIN"
                }
            ],
            productSuppliers:
            [
                new ProductSupplier
                {
                    ProductId = productId,
                    SupplierId = supplierId,
                    IsDefault = true,
                    UnitCost = 750m
                }
            ],
            purchaseOrders:
            [
                new PurchaseOrder
                {
                    PurchaseOrderId = existingPurchaseOrderId,
                    PurchaseOrderNumber = "PO-EXISTING",
                    WarehouseId = warehouseId,
                    SupplierId = supplierId,
                    Status = PurchaseOrderStatus.Approved
                }
            ],
            purchaseOrderLines:
            [
                new PurchaseOrderLine
                {
                    PurchaseOrderId = existingPurchaseOrderId,
                    ProductId = productId,
                    QuantityOrdered = 10,
                    QuantityReceived = 4,
                    UnitCost = 750m
                }
            ],
            generatedNumbers: ["PO-NEW"]);

        var sut = context.CreateSut();

        await sut.EvaluateLowStockAsync(warehouseId, productId);

        Assert.Single(context.PurchaseOrders.Items);
        Assert.Single(context.PurchaseOrderLines.Items);
        Assert.Equal(0, context.DocumentNumberGenerator.CallCount);
    }

    [Fact]
    public async Task EvaluateLowStockAsync_WhenNoDefaultSupplierExists_DoesNotCreatePurchaseOrder()
    {
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var context = LowStockTestContext.Create(
            inventories:
            [
                new WarehouseInventory
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    QuantityOnHand = 9,
                    ReservedQuantity = 3,
                    ReorderThresholdOverride = 6
                }
            ],
            products:
            [
                new Product
                {
                    ProductId = productId,
                    Name = "Sugar",
                    Sku = "SUGAR-001",
                    ReorderThreshold = 6,
                    ReorderQuantity = 18
                }
            ],
            warehouses:
            [
                new Warehouse
                {
                    WarehouseId = warehouseId,
                    Name = "Main Warehouse",
                    Code = "WH-MAIN"
                }
            ]);

        var sut = context.CreateSut();

        await sut.EvaluateLowStockAsync(warehouseId, productId);

        Assert.Empty(context.PurchaseOrders.Items);
        Assert.Empty(context.PurchaseOrderLines.Items);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(0, context.DocumentNumberGenerator.CallCount);
    }

    private sealed class LowStockTestContext
    {
        private readonly InMemoryRepository<WarehouseInventory> inventoryRepository;
        private readonly InMemoryRepository<Product> productRepository;
        private readonly InMemoryRepository<Warehouse> warehouseRepository;
        private readonly InMemoryRepository<ProductSupplier> productSupplierRepository;

        private LowStockTestContext(
            InMemoryRepository<WarehouseInventory> inventoryRepository,
            InMemoryRepository<Product> productRepository,
            InMemoryRepository<Warehouse> warehouseRepository,
            InMemoryRepository<ProductSupplier> productSupplierRepository,
            InMemoryRepository<PurchaseOrder> purchaseOrderRepository,
            InMemoryRepository<PurchaseOrderLine> purchaseOrderLineRepository,
            TestUnitOfWork unitOfWork,
            TestDocumentNumberGenerator documentNumberGenerator,
            TestUserManager userManager,
            TestEmailBackgroundJobs emailBackgroundJobs)
        {
            this.inventoryRepository = inventoryRepository;
            this.productRepository = productRepository;
            this.warehouseRepository = warehouseRepository;
            this.productSupplierRepository = productSupplierRepository;
            PurchaseOrders = purchaseOrderRepository;
            PurchaseOrderLines = purchaseOrderLineRepository;
            UnitOfWork = unitOfWork;
            DocumentNumberGenerator = documentNumberGenerator;
            UserManager = userManager;
            EmailBackgroundJobs = emailBackgroundJobs;
        }

        public InMemoryRepository<PurchaseOrder> PurchaseOrders { get; }
        public InMemoryRepository<PurchaseOrderLine> PurchaseOrderLines { get; }
        public TestUnitOfWork UnitOfWork { get; }
        public TestDocumentNumberGenerator DocumentNumberGenerator { get; }
        public TestUserManager UserManager { get; }
        public TestEmailBackgroundJobs EmailBackgroundJobs { get; }

        public static LowStockTestContext Create(
            IEnumerable<WarehouseInventory> inventories,
            IEnumerable<Product> products,
            IEnumerable<Warehouse> warehouses,
            IEnumerable<ProductSupplier>? productSuppliers = null,
            IEnumerable<PurchaseOrder>? purchaseOrders = null,
            IEnumerable<PurchaseOrderLine>? purchaseOrderLines = null,
            IEnumerable<string>? generatedNumbers = null)
        {
            var inventoryRepository = new InMemoryRepository<WarehouseInventory>(inventories);
            var productRepository = new InMemoryRepository<Product>(products);
            var warehouseRepository = new InMemoryRepository<Warehouse>(warehouses);
            var productSupplierRepository = new InMemoryRepository<ProductSupplier>(productSuppliers ?? []);
            var purchaseOrderRepository = new InMemoryRepository<PurchaseOrder>(purchaseOrders ?? []);
            var purchaseOrderLineRepository = new InMemoryRepository<PurchaseOrderLine>(purchaseOrderLines ?? []);

            var unitOfWork = new TestUnitOfWork();
            unitOfWork.Register(inventoryRepository);
            unitOfWork.Register(productRepository);
            unitOfWork.Register(warehouseRepository);
            unitOfWork.Register(productSupplierRepository);
            unitOfWork.Register(purchaseOrderRepository);
            unitOfWork.Register(purchaseOrderLineRepository);

            return new LowStockTestContext(
                inventoryRepository,
                productRepository,
                warehouseRepository,
                productSupplierRepository,
                purchaseOrderRepository,
                purchaseOrderLineRepository,
                unitOfWork,
                new TestDocumentNumberGenerator(generatedNumbers ?? ["PO-DEFAULT"]),
                new TestUserManager(),
                new TestEmailBackgroundJobs());
        }

        public LowStockBackgroundJobs CreateSut()
        {
            UnitOfWork.Register(inventoryRepository);
            UnitOfWork.Register(productRepository);
            UnitOfWork.Register(warehouseRepository);
            UnitOfWork.Register(productSupplierRepository);
            UnitOfWork.Register(PurchaseOrders);
            UnitOfWork.Register(PurchaseOrderLines);

            return new LowStockBackgroundJobs(
                UnitOfWork,
                DocumentNumberGenerator,
                UserManager,
                EmailBackgroundJobs,
                NullLogger<LowStockBackgroundJobs>.Instance);
        }
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> repositories = [];

        public int SaveChangesCallCount { get; private set; }

        public void Register<T>(IBaseRepository<T> repository) where T : class
        {
            repositories[typeof(T)] = repository;
        }

        public IBaseRepository<T> Repository<T>() where T : class
        {
            return repositories.TryGetValue(typeof(T), out var repository)
                ? (IBaseRepository<T>)repository
                : throw new InvalidOperationException($"No repository registered for {typeof(T).Name}.");
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class InMemoryRepository<T>(IEnumerable<T> seed) : IBaseRepository<T> where T : class
    {
        public List<T> Items { get; } = [.. seed];

        public Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            Items.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(T entity)
        {
        }

        public void Remove(T entity)
        {
            Items.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities.ToList())
            {
                Items.Remove(entity);
            }
        }

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(entity => GetEntityId(entity) == id));

        public Task<T?> GetByIdAsync(Guid id, Func<IQueryable<T>, IQueryable<T>> include, CancellationToken cancellationToken = default)
            => GetByIdAsync(id, cancellationToken);

        public Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Items.ToList());

        public Task<T?> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));

        public Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.AsQueryable().Where(predicate).ToList());

        public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.AsQueryable().Any(predicate));

        public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(predicate is null ? Items.Count : Items.AsQueryable().Count(predicate));

        public Task<PaginationResult<T>> GetPagedItemsAsync(
            RequestParameters parameters,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default,
            Func<IQueryable<T>, IQueryable<T>>? include = null)
        {
            throw new NotSupportedException("Paged queries are not needed in these tests.");
        }

        public Task<PaginationResult<TResult>> GetPagedProjectionAsync<TResult>(
            RequestParameters parameters,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            Expression<Func<T, TResult>> selector,
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Paged projections are not needed in these tests.");
        }

        public Task<List<T>> GetPagedListAsync(int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Skip(skip).Take(take).ToList());

        public Task<List<T>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Items.ToList());

        public Task<T?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
            => GetByIdAsync(id, cancellationToken);

        public Task<List<T>> WhereIncludingDeletedAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => WhereAsync(predicate, cancellationToken);

        private static Guid GetEntityId(T entity)
        {
            var entityType = typeof(T);
            var idProperty = entityType.GetProperty($"{entityType.Name}Id")
                ?? entityType.GetProperties()
                    .FirstOrDefault(property => property.PropertyType == typeof(Guid) && property.Name.EndsWith("Id", StringComparison.Ordinal));

            if (idProperty is null)
            {
                throw new InvalidOperationException($"Unable to resolve a Guid key property for {entityType.Name}.");
            }

            return (Guid)(idProperty.GetValue(entity) ?? Guid.Empty);
        }
    }

    private sealed class TestDocumentNumberGenerator(IEnumerable<string> generatedNumbers) : IDocumentNumberGenerator
    {
        private readonly Queue<string> numbers = new(generatedNumbers);

        public int CallCount { get; private set; }

        public string GeneratePurchaseOrderNumber()
        {
            CallCount++;

            return numbers.Count > 0
                ? numbers.Dequeue()
                : $"PO-{CallCount:0000}";
        }

        public string GenerateTransferNumber() => throw new NotSupportedException();
        public string GenerateAdjustmentNumber() => throw new NotSupportedException();
        public string GenerateOpeningBalanceNumber() => throw new NotSupportedException();
        public string GenerateSalesOrderNumber() => throw new NotSupportedException();
    }

    private sealed class TestEmailBackgroundJobs : IEmailBackgroundJobs
    {
        public List<string> LowStockAlerts { get; } = [];
        public List<string> ManualReviews { get; } = [];

        public Task SendWelcomeEmailAsync(string toEmail, string firstName, string lastName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string resetLink, int expirationHours, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendPasswordChangedEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendPasswordResetSuccessEmailAsync(string toEmail, string firstName, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendLowStockAlertEmailAsync(
            string toEmail,
            string recipientName,
            string warehouseName,
            string warehouseCode,
            string productName,
            string sku,
            int availableQuantity,
            int threshold,
            int reorderQuantity,
            CancellationToken cancellationToken = default)
        {
            LowStockAlerts.Add(toEmail);
            return Task.CompletedTask;
        }

        public Task SendManualProcurementReviewEmailAsync(
            string toEmail,
            string recipientName,
            string warehouseName,
            string warehouseCode,
            string productName,
            string sku,
            int availableQuantity,
            int threshold,
            CancellationToken cancellationToken = default)
        {
            ManualReviews.Add(toEmail);
            return Task.CompletedTask;
        }

        public Task SendPurchaseOrderApprovedEmailAsync(
            string toEmail,
            string supplierName,
            string purchaseOrderNumber,
            string warehouseName,
            DateTime? expectedArrivalDate,
            string lineSummary,
            string? notes,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestUserManager : UserManager<ApplicationUser>
    {
        public TestUserManager()
            : base(
                new TestUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                [],
                [],
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<ApplicationUser>>.Instance)
        {
        }

        public override Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName)
            => Task.FromResult<IList<ApplicationUser>>([]);
    }

    private sealed class TestUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
