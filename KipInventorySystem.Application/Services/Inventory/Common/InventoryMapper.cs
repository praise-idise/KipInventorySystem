using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;
using KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;
using KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;
using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;
using KipInventorySystem.Domain.Entities;
using Mapster;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public class InventoryMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Supplier, SupplierDto>();
        config.NewConfig<Warehouse, WarehouseDto>();
        config.NewConfig<Product, ProductDto>();

        config.NewConfig<PurchaseOrderLine, PurchaseOrderLineDto>();
        config.NewConfig<PurchaseOrder, PurchaseOrderDto>();

        config.NewConfig<TransferRequestLine, TransferRequestLineDto>();
        config.NewConfig<TransferRequest, TransferRequestDto>();

        config.NewConfig<StockAdjustmentLine, StockAdjustmentLineDto>();
        config.NewConfig<StockAdjustment, StockAdjustmentDto>();

        config.NewConfig<CreateSupplierRequest, Supplier>()
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.Email, src => Normalize(src.Email))
            .Map(dest => dest.Phone, src => Normalize(src.Phone))
            .Map(dest => dest.ContactPerson, src => Normalize(src.ContactPerson));

        config.NewConfig<CreateWarehouseRequest, Warehouse>()
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.State, src => src.State.Trim())
            .Map(dest => dest.Location, src => Normalize(src.Location));

        config.NewConfig<CreateProductRequest, Product>()
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.Description, src => Normalize(src.Description))
            .Map(dest => dest.UnitOfMeasure, src => src.UnitOfMeasure.Trim());

        config.NewConfig<CreatePurchaseOrderDraftRequest, PurchaseOrder>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes));

        config.NewConfig<CreatePurchaseOrderLineRequest, PurchaseOrderLine>();

        config.NewConfig<CreateTransferRequestDraftRequest, TransferRequest>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes));

        config.NewConfig<CreateTransferRequestLineRequest, TransferRequestLine>();

        config.NewConfig<CreateStockAdjustmentDraftRequest, StockAdjustment>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
