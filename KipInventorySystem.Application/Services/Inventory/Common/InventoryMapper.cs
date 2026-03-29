using KipInventorySystem.Application.Services.Inventory.Approvals.DTOs;
using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;
using KipInventorySystem.Application.Services.Inventory.ProductSuppliers.DTOs;
using KipInventorySystem.Application.Services.Inventory.Products.DTOs;
using KipInventorySystem.Application.Services.Inventory.PurchaseOrders.DTOs;
using KipInventorySystem.Application.Services.Inventory.SalesOrders.DTOs;
using KipInventorySystem.Application.Services.Inventory.StockAdjustments.DTOs;
using KipInventorySystem.Application.Services.Inventory.Suppliers.DTOs;
using KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;
using KipInventorySystem.Application.Services.Inventory.Warehouses.DTOs;
using KipInventorySystem.Application.Services.Inventory.OpeningBalances.DTOs;
using KipInventorySystem.Domain.Entities;
using Mapster;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public class InventoryMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        #region SUPPLIER MAPPINGS
        config.NewConfig<Supplier, SupplierDto>();
        config.NewConfig<CreateSupplierRequest, Supplier>()
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.Email, src => Normalize(src.Email))
            .Map(dest => dest.Phone, src => Normalize(src.Phone))
            .Map(dest => dest.ContactPerson, src => Normalize(src.ContactPerson));
        #endregion

        #region WAREHOUSE MAPPINGS
        config.NewConfig<Warehouse, WarehouseDto>()
            .Ignore(dest => dest.InventoryItems);
        config.NewConfig<WarehouseInventory, WarehouseInventoryItemDto>()
            .Map(dest => dest.ProductName, src => src.Product.Name)
            .Map(dest => dest.Sku, src => src.Product.Sku)
            .Map(dest => dest.UnitOfMeasure, src => src.Product.UnitOfMeasure);
        config.NewConfig<CreateWarehouseRequest, Warehouse>()
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.State, src => src.State.Trim())
            .Map(dest => dest.Location, src => Normalize(src.Location));
        #endregion

        #region PRODUCT MAPPINGS
        config.NewConfig<ProductVariantAttribute, ProductVariantAttributeDTO>();
        config.NewConfig<ProductSupplier, ProductSupplierDTO>()
            .Map(dest => dest.SupplierName, src => src.Supplier.Name)
            .Map(dest => dest.SupplierEmail, src => src.Supplier.Email);
        config.NewConfig<Product, ProductDTO>()
            .Map(dest => dest.VariantAttributes, src => src.VariantAttributes.OrderBy(x => x.SortOrder))
            .Map(dest => dest.Suppliers, src => src.ProductSuppliers.OrderByDescending(x => x.IsDefault).ThenBy(x => x.SupplierId));
        config.NewConfig<CreateProductVariantAttributeDTO, ProductVariantAttribute>()
            .Map(dest => dest.AttributeName, src => src.AttributeName.Trim())
            .Map(dest => dest.AttributeCode, src => src.AttributeCode.Trim().ToUpperInvariant());
        config.NewConfig<CreateProductDTO, Product>()
            .Map(dest => dest.CategoryCode, src => src.CategoryCode.Trim().ToUpperInvariant())
            .Map(dest => dest.BrandCode, src => src.BrandCode.Trim().ToUpperInvariant())
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.Description, src => Normalize(src.Description))
            .Map(dest => dest.UnitOfMeasure, src => src.UnitOfMeasure.Trim())
            .Map(dest => dest.VariantAttributes, src => src.VariantAttributes);
        #endregion

        #region PURCHASE ORDER MAPPINGS
        config.NewConfig<PurchaseOrderLine, PurchaseOrderLineDTO>()
            .Map(dest => dest.ProductName, src => src.Product.Name)
            .Map(dest => dest.Sku, src => src.Product.Sku);
        config.NewConfig<PurchaseOrder, PurchaseOrderDTO>()
            .Map(dest => dest.WarehouseName, src => src.Warehouse.Name)
            .Map(dest => dest.WarehouseState, src => src.Warehouse.State);
        config.NewConfig<CreatePurchaseOrderDraftRequest, PurchaseOrder>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes))
            .Ignore(dest => dest.Lines);
        config.NewConfig<CreatePurchaseOrderLineRequest, PurchaseOrderLine>();
        #endregion

        #region TRANSFER REQUEST MAPPINGS
        config.NewConfig<TransferRequestLine, TransferRequestLineDto>();
        config.NewConfig<TransferRequest, TransferRequestDto>();
        config.NewConfig<CreateTransferRequestDraftRequest, TransferRequest>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes));
        config.NewConfig<CreateTransferRequestLineRequest, TransferRequestLine>();
        #endregion

        #region STOCK ADJUSTMENT MAPPINGS
        config.NewConfig<StockAdjustmentLine, StockAdjustmentLineDto>();
        config.NewConfig<StockAdjustment, StockAdjustmentDto>();
        config.NewConfig<CreateStockAdjustmentDraftRequest, StockAdjustment>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes))
            .Ignore(dest => dest.Lines);
        #endregion

        #region OPENING BALANCE MAPPINGS
        config.NewConfig<OpeningBalanceLine, OpeningBalanceLineDto>();
        config.NewConfig<OpeningBalance, OpeningBalanceDto>();
        config.NewConfig<CreateOpeningBalanceRequest, OpeningBalance>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes))
            .Ignore(dest => dest.Lines);
        #endregion

        #region APPROVAL MAPPINGS
        config.NewConfig<ApprovalRequest, ApprovalRequestDto>();
        #endregion

        #region CUSTOMER MAPPINGS
        config.NewConfig<Customer, CustomerDto>();
        config.NewConfig<CreateCustomerRequest, Customer>()
            .Map(dest => dest.Name, src => src.Name.Trim())
            .Map(dest => dest.Email, src => Normalize(src.Email))
            .Map(dest => dest.Phone, src => Normalize(src.Phone));
        #endregion

        #region SALES ORDER MAPPINGS
        config.NewConfig<SalesOrderLine, SalesOrderLineDto>()
            .Map(dest => dest.ProductName, src => src.Product.Name)
            .Map(dest => dest.Sku, src => src.Product.Sku);
        config.NewConfig<SalesOrder, SalesOrderDto>()
            .Map(dest => dest.CustomerName, src => src.Customer.Name);
        config.NewConfig<CreateSalesOrderDraftRequest, SalesOrder>()
            .Map(dest => dest.Notes, src => Normalize(src.Notes))
            .Ignore(dest => dest.CustomerId)
            .Ignore(dest => dest.Customer)
            .Ignore(dest => dest.Lines);
        config.NewConfig<CreateSalesOrderLineRequest, SalesOrderLine>();
        #endregion
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
