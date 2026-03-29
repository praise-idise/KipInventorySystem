using KipInventorySystem.Application.Services.Inventory.Customers.DTOs;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Customers;

public class CustomerService(
    IUnitOfWork unitOfWork,
    IIdempotencyService idempotencyService,
    IUserContext userContext,
    IMapper mapper,
    ILogger<CustomerService> logger) : ICustomerService
{
    public Task<ServiceResponse<CustomerDto>> CreateAsync(
        CreateCustomerRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return idempotencyService.ExecuteAsync(
            "customer-create",
            idempotencyKey,
            request,
            async token =>
            {
                var customerRepo = unitOfWork.Repository<Customer>();
                var customer = mapper.Map<Customer>(request);

                var existing = await customerRepo.ExistsAsync(
                    x => x.Email != null && customer.Email != null && x.Email == customer.Email,
                    token);

                if (existing)
                {
                    return ServiceResponse<CustomerDto>.Conflict(
                        $"A customer with email '{customer.Email}' already exists.");
                }

                customer.CreatedAt = DateTime.UtcNow;
                customer.UpdatedAt = DateTime.UtcNow;
                await customerRepo.AddAsync(customer, token);
                await unitOfWork.SaveChangesAsync(token);

                logger.LogInformation(
                    "Inventory audit: operation={Operation}, actor={Actor}, entity=Customer, entityId={EntityId}",
                    "CreateCustomer",
                    userContext.GetCurrentUser().UserId,
                    customer.CustomerId);

                return ServiceResponse<CustomerDto>.Created(
                    mapper.Map<CustomerDto>(customer),
                    "Customer created successfully.");
            },
            cancellationToken);
    }

    public async Task<ServiceResponse<CustomerDto>> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerRepo = unitOfWork.Repository<Customer>();
        var customer = await customerRepo.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return ServiceResponse<CustomerDto>.NotFound("Customer was not found.");
        }

        if (request.Name is not null)
        {
            customer.Name = request.Name.Trim();
        }

        if (request.Email is not null)
        {
            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            if (!string.IsNullOrWhiteSpace(email))
            {
                var duplicate = await customerRepo.ExistsAsync(
                    x => x.CustomerId != customerId && x.Email == email,
                    cancellationToken);

                if (duplicate)
                {
                    return ServiceResponse<CustomerDto>.Conflict($"A customer with email '{email}' already exists.");
                }
            }

            customer.Email = email;
        }

        if (request.Phone is not null)
        {
            customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        }

        customer.UpdatedAt = DateTime.UtcNow;
        customerRepo.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Customer, entityId={EntityId}",
            "UpdateCustomer",
            userContext.GetCurrentUser().UserId,
            customer.CustomerId);

        return ServiceResponse<CustomerDto>.Success(
            mapper.Map<CustomerDto>(customer),
            "Customer updated successfully.");
    }

    public async Task<ServiceResponse> SoftDeleteAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customerRepo = unitOfWork.Repository<Customer>();
        var customer = await customerRepo.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return ServiceResponse.NotFound("Customer was not found.");
        }

        customer.IsDeleted = true;
        customer.IsActive = false;
        customer.DeletedAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;
        customerRepo.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Inventory audit: operation={Operation}, actor={Actor}, entity=Customer, entityId={EntityId}",
            "DeleteCustomer",
            userContext.GetCurrentUser().UserId,
            customer.CustomerId);

        return ServiceResponse.Success("Customer deleted successfully.");
    }

    public async Task<ServiceResponse<CustomerDto>> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await unitOfWork.Repository<Customer>().GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return ServiceResponse<CustomerDto>.NotFound("Customer was not found.");
        }

        return ServiceResponse<CustomerDto>.Success(mapper.Map<CustomerDto>(customer));
    }

    public async Task<ServiceResponse<PaginationResult<CustomerDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var customers = await unitOfWork.Repository<Customer>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            cancellationToken: cancellationToken);

        var response = new PaginationResult<CustomerDto>
        {
            Records = customers.Records.Select(x => mapper.Map<CustomerDto>(x)).ToList(),
            TotalRecords = customers.TotalRecords,
            PageSize = customers.PageSize,
            CurrentPage = customers.CurrentPage
        };

        return ServiceResponse<PaginationResult<CustomerDto>>.Success(response);
    }

    public async Task<ServiceResponse<PaginationResult<CustomerDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(parameters, cancellationToken);
        }

        var pattern = $"%{searchTerm.Trim()}%";
        var customers = await unitOfWork.Repository<Customer>().GetPagedItemsAsync(
            parameters,
            query => query.OrderByDescending(x => x.CreatedAt),
            x => EF.Functions.ILike(x.Name, pattern) ||
                 (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                 (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)),
            cancellationToken);

        var response = new PaginationResult<CustomerDto>
        {
            Records = customers.Records.Select(x => mapper.Map<CustomerDto>(x)).ToList(),
            TotalRecords = customers.TotalRecords,
            PageSize = customers.PageSize,
            CurrentPage = customers.CurrentPage
        };

        return ServiceResponse<PaginationResult<CustomerDto>>.Success(response);
    }
}
