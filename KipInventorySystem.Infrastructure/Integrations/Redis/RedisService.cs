using KipInventorySystem.Application.Services.Redis;
using StackExchange.Redis;

namespace KipInventorySystem.Infrastructure.Integrations.Redis;

public class RedisService(IConnectionMultiplexer redis)
    : IRedisService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            await _db.StringSetAsync(key, value, expiry);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry)
    {
        try
        {
            return await _db.StringSetAsync(key, value, expiry, when: When.NotExists);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await _db.StringGetAsync(key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task AddToSetAsync(string key, string value)
    {
        try
        {
            await _db.SetAddAsync(key, value);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task ExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            await _db.KeyExpireAsync(key, expiry);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task RemoveFromSetAsync(string key, string value)
    {
        try
        {
            await _db.SetRemoveAsync(key, value);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }

    public async Task<IReadOnlyCollection<string>> GetSetMembersAsync(string key)
    {
        try
        {
            return [.. (await _db.SetMembersAsync(key)).Select(x => x.ToString())];
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            throw new RedisUnavailableException("Redis is unavailable.", ex);
        }
    }
}

