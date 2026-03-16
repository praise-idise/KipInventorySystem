using KipInventorySystem.Application.Services.Auth;
using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Inventory.GoodsReceipts;
using KipInventorySystem.Application.Services.Inventory.Products;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments;
using KipInventorySystem.Application.Services.Inventory.StockIssues;
using KipInventorySystem.Application.Services.Inventory.Suppliers;
using KipInventorySystem.Application.Services.Inventory.TransferRequests;
using KipInventorySystem.Application.Services.Inventory.Warehouses;
using KipInventorySystem.Application.Validators.Auth;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace KipInventorySystem.Application.Extensions;

public static class ServiceCollectionExtension
{
    public static void AddApplication(this IServiceCollection services, WebApplicationBuilder builder)
    {
        // Register your application services here
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(AuthMapper).Assembly);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailBackgroundJobs, EmailBackgroundJobs>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductSupplierService, ProductSupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IInventorySupplierService, InventorySupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<IStockIssueService, StockIssueService>();
        services.AddScoped<ITransferRequestService, TransferRequestService>();
        services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddSingleton<IDocumentNumberGenerator, DocumentNumberGenerator>();
        services.AddScoped<ILowStockBackgroundJobs, LowStockBackgroundJobs>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        services.AddValidatorsFromAssemblyContaining<RegisterDTOValidator>();
        services.AddFluentValidationAutoValidation();

    }
}
