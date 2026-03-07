using System.Data;
using System.Diagnostics;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Shared.Responses;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KipInventorySystem.Infrastructure.Services.Inventory;

public class InventoryTransactionRunner(
    IUnitOfWork unitOfWork,
    ILogger<InventoryTransactionRunner> logger) : IInventoryTransactionRunner
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(120);

    public Task<ServiceResponse> ExecuteSerializableAsync(
        string operationName,
        Func<CancellationToken, Task<ServiceResponse>> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, operation, cancellationToken);
    }

    public Task<ServiceResponse<T>> ExecuteSerializableAsync<T>(
        string operationName,
        Func<CancellationToken, Task<ServiceResponse<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, operation, cancellationToken);
    }

    private async Task<TResponse> ExecuteCoreAsync<TResponse>(
        string operationName,
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
        where TResponse : ServiceResponseBase
    {
        var sw = Stopwatch.StartNew();
        var operationTag = new KeyValuePair<string, object?>("operation", operationName);
        InventoryTelemetry.CommandCounter.Add(1, operationTag);

        using var activity = InventoryTelemetry.ActivitySource.StartActivity($"inventory.{operationName}");
        activity?.SetTag("inventory.operation", operationName);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                var response = await operation(cancellationToken);
                if (response.Succeeded)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await unitOfWork.CommitTransactionAsync(cancellationToken);
                }
                else
                {
                    await unitOfWork.RollbackTransactionAsync(cancellationToken);
                }

                InventoryTelemetry.CommandDurationMs.Record(sw.Elapsed.TotalMilliseconds, operationTag);
                activity?.SetTag("inventory.success", response.Succeeded);
                return response;
            }
            catch (Exception ex) when (IsRetryableSerializationFailure(ex) && attempt < MaxRetries)
            {
                InventoryTelemetry.CommandRetryCounter.Add(1, operationTag);
                logger.LogWarning(
                    ex,
                    "Retryable inventory transaction failure for {OperationName}. Attempt {Attempt}/{MaxAttempts}.",
                    operationName,
                    attempt,
                    MaxRetries);

                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * attempt), cancellationToken);
            }
            catch (Exception ex) when (IsRetryableSerializationFailure(ex))
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                InventoryTelemetry.CommandFailureCounter.Add(1, operationTag);
                InventoryTelemetry.CommandDurationMs.Record(sw.Elapsed.TotalMilliseconds, operationTag);
                logger.LogWarning(
                    ex,
                    "Inventory transaction concurrency conflict for {OperationName} after retries.",
                    operationName);

                return CreateConflict<TResponse>(
                    "The operation conflicted with another concurrent transaction. Please retry.");
            }
            catch (Exception ex)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                InventoryTelemetry.CommandFailureCounter.Add(1, operationTag);
                InventoryTelemetry.CommandDurationMs.Record(sw.Elapsed.TotalMilliseconds, operationTag);

                logger.LogError(ex, "Unhandled inventory transaction error for {OperationName}.", operationName);
                return CreateError<TResponse>("An unexpected inventory transaction error occurred.");
            }
        }

        InventoryTelemetry.CommandFailureCounter.Add(1, operationTag);
        InventoryTelemetry.CommandDurationMs.Record(sw.Elapsed.TotalMilliseconds, operationTag);
        return CreateConflict<TResponse>("The operation conflicted with another concurrent transaction. Please retry.");
    }

    private static bool IsRetryableSerializationFailure(Exception exception)
    {
        if (exception is NpgsqlException npgsqlException)
        {
            return IsRetryableNpgsqlState(npgsqlException.SqlState);
        }

        if (exception.InnerException is NpgsqlException innerNpgsql)
        {
            return IsRetryableNpgsqlState(innerNpgsql.SqlState);
        }

        return false;
    }

    private static bool IsRetryableNpgsqlState(string? sqlState)
    {
        return sqlState is "40001" or "40P01";
    }

    private static TResponse CreateError<TResponse>(string message) where TResponse : ServiceResponseBase
    {
        if (typeof(TResponse) == typeof(ServiceResponse))
        {
            return (TResponse)(object)ServiceResponse.Error(message);
        }

        var genericType = typeof(TResponse);
        var method = typeof(ServiceResponseBase)
            .GetMethod(nameof(ServiceResponseBase.Error))?
            .MakeGenericMethod(genericType);

        if (method?.Invoke(null, [message]) is TResponse typed)
        {
            return typed;
        }

        throw new InvalidOperationException($"Unable to create error response for type {typeof(TResponse).Name}");
    }

    private static TResponse CreateConflict<TResponse>(string message) where TResponse : ServiceResponseBase
    {
        if (typeof(TResponse) == typeof(ServiceResponse))
        {
            return (TResponse)(object)ServiceResponse.Conflict(message);
        }

        var genericType = typeof(TResponse);
        var method = typeof(ServiceResponseBase)
            .GetMethod(nameof(ServiceResponseBase.Conflict))?
            .MakeGenericMethod(genericType);

        if (method?.Invoke(null, [message]) is TResponse typed)
        {
            return typed;
        }

        throw new InvalidOperationException($"Unable to create conflict response for type {typeof(TResponse).Name}");
    }
}
