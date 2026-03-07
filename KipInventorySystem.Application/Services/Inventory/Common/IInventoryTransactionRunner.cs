using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public interface IInventoryTransactionRunner
{
    Task<ServiceResponse> ExecuteSerializableAsync(
        string operationName,
        Func<CancellationToken, Task<ServiceResponse>> operation,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<T>> ExecuteSerializableAsync<T>(
        string operationName,
        Func<CancellationToken, Task<ServiceResponse<T>>> operation,
        CancellationToken cancellationToken = default);
}
