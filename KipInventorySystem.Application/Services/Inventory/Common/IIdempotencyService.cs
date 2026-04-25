using KipInventorySystem.Shared.Models;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public interface IIdempotencyService
{
    Task<ServiceResponse> ExecuteAsync<TRequest>(
        string operationName,
        string idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<ServiceResponse>> action,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<TResponse>> ExecuteAsync<TRequest, TResponse>(
        string operationName,
        string idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<ServiceResponse<TResponse>>> action,
        CancellationToken cancellationToken = default);
}
