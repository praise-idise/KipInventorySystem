using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.Customers;

public interface ICustomerService
{
    Task<ServiceResponse<CustomerDto>> CreateAsync(
        CreateCustomerRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<CustomerDto>> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> SoftDeleteAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<CustomerDto>> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<ServiceResponse<PaginationResult<CustomerDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<CustomerDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
