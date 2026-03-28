using KipInventorySystem.Application.Services.Inventory.SalesOrders.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.SalesOrders;

public interface ISalesOrderService
{
    Task<ServiceResponse<SalesOrderDto>> CreateDraftAsync(
        CreateSalesOrderDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<SalesOrderDto>> UpdateDraftAsync(
        Guid salesOrderId,
        UpdateSalesOrderDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<SalesOrderDto>> ConfirmAsync(
        Guid salesOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<SalesOrderDto>> FulfillAsync(
        Guid salesOrderId,
        FulfillSalesOrderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<SalesOrderDto>> CancelAsync(
        Guid salesOrderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<SalesOrderDto>> GetByIdAsync(Guid salesOrderId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<SalesOrderDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<SalesOrderDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
