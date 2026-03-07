using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KipInventorySystem.Application.Services.Redis;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Responses;
using Microsoft.Extensions.Logging;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public class IdempotencyService(
    IRedisService redis,
    IUserContext userContext,
    ILogger<IdempotencyService> logger) : IIdempotencyService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private const int InProgressTtlMinutes = 5;
    private const int CompletedTtlHours = 24;

    public Task<ServiceResponse> ExecuteAsync<TRequest>(
        string operationName,
        string idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<ServiceResponse>> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, idempotencyKey, request, action, cancellationToken);
    }

    public Task<ServiceResponse<TResponse>> ExecuteAsync<TRequest, TResponse>(
        string operationName,
        string idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<ServiceResponse<TResponse>>> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(operationName, idempotencyKey, request, action, cancellationToken);
    }

    private async Task<TResponse> ExecuteCoreAsync<TRequest, TResponse>(
        string operationName,
        string idempotencyKey,
        TRequest request,
        Func<CancellationToken, Task<TResponse>> action,
        CancellationToken cancellationToken)
        where TResponse : ServiceResponseBase
    {
        var userId = userContext.GetCurrentUser().UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "anonymous";
        }

        var key = $"idem:inventory:{operationName}:{userId}:{idempotencyKey}";
        var payloadHash = ComputePayloadHash(request);

        var existingRecordJson = await redis.GetAsync(key);
        if (!string.IsNullOrWhiteSpace(existingRecordJson))
        {
            var existingRecord = JsonSerializer.Deserialize<IdempotencyRecord>(existingRecordJson, _jsonOptions);
            if (existingRecord != null)
            {
                var existingResult = ParseExistingRecord<TResponse>(existingRecord, payloadHash, key);
                if (existingResult != null)
                {
                    return existingResult;
                }
            }
        }

        var inProgress = new IdempotencyRecord
        {
            State = IdempotencyRecordState.InProgress,
            PayloadHash = payloadHash,
            ResponseJson = null
        };

        var setSucceeded = await redis.SetIfNotExistsAsync(
            key,
            JsonSerializer.Serialize(inProgress, _jsonOptions),
            TimeSpan.FromMinutes(InProgressTtlMinutes));

        if (!setSucceeded)
        {
            existingRecordJson = await redis.GetAsync(key);
            if (!string.IsNullOrWhiteSpace(existingRecordJson))
            {
                var existingRecord = JsonSerializer.Deserialize<IdempotencyRecord>(existingRecordJson, _jsonOptions);
                if (existingRecord != null)
                {
                    var existingResult = ParseExistingRecord<TResponse>(existingRecord, payloadHash, key);
                    if (existingResult != null)
                    {
                        return existingResult;
                    }
                }
            }

            return CreateConflict<TResponse>("An idempotent request with this key is already in progress.");
        }

        var response = await action(cancellationToken);

        var completedRecord = new IdempotencyRecord
        {
            State = IdempotencyRecordState.Completed,
            PayloadHash = payloadHash,
            ResponseJson = JsonSerializer.Serialize(response, _jsonOptions)
        };

        await redis.SetAsync(
            key,
            JsonSerializer.Serialize(completedRecord, _jsonOptions),
            TimeSpan.FromHours(CompletedTtlHours));

        return response;
    }

    private TResponse? ParseExistingRecord<TResponse>(IdempotencyRecord existingRecord, string payloadHash, string key)
        where TResponse : ServiceResponseBase
    {
        if (!string.Equals(existingRecord.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            return CreateConflict<TResponse>("The provided idempotency key was used with a different payload.");
        }

        if (existingRecord.State == IdempotencyRecordState.Completed && !string.IsNullOrWhiteSpace(existingRecord.ResponseJson))
        {
            var cached = JsonSerializer.Deserialize<TResponse>(existingRecord.ResponseJson, _jsonOptions);
            if (cached != null)
            {
                logger.LogInformation("Idempotency replay served from cache for key {IdempotencyKey}", key);
                return cached;
            }
        }

        if (existingRecord.State == IdempotencyRecordState.InProgress)
        {
            return CreateConflict<TResponse>("An idempotent request with this key is already in progress.");
        }

        return null;
    }

    private static string ComputePayloadHash<TRequest>(TRequest request)
    {
        var payload = JsonSerializer.Serialize(request);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    private static TResponse CreateConflict<TResponse>(string message)
        where TResponse : ServiceResponseBase
    {
        if (typeof(TResponse) == typeof(ServiceResponse))
        {
            return (TResponse)(object)ServiceResponseBase.Conflict<ServiceResponse>(message);
        }

        var genericType = typeof(TResponse);
        var method = typeof(ServiceResponseBase)
            .GetMethod(nameof(ServiceResponseBase.Conflict))?
            .MakeGenericMethod(genericType);

        if (method != null)
        {
            var value = method.Invoke(null, [message]);
            if (value is TResponse typed)
            {
                return typed;
            }
        }

        throw new InvalidOperationException($"Unable to create conflict response for type {typeof(TResponse).Name}");
    }

    private sealed class IdempotencyRecord
    {
        public string State { get; set; } = IdempotencyRecordState.InProgress;
        public string PayloadHash { get; set; } = string.Empty;
        public string? ResponseJson { get; set; }
    }

    private static class IdempotencyRecordState
    {
        public const string InProgress = "in_progress";
        public const string Completed = "completed";
    }
}
