using KipInventorySystem.Application.Services.Redis;
using StackExchange.Redis;

namespace KipInventorySystem.Infrastructure.Integrations.Redis;

public class RedisService(IConnectionMultiplexer redis)
    : IRedisService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
        => await _db.StringSetAsync(key, value, expiry);

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry)
        => await _db.StringSetAsync(key, value, expiry, when: When.NotExists);

    public async Task<string?> GetAsync(string key)
        => await _db.StringGetAsync(key);

    public async Task RemoveAsync(string key)
        => await _db.KeyDeleteAsync(key);

    public async Task AddToSetAsync(string key, string value)
        => await _db.SetAddAsync(key, value);

    public async Task ExpireAsync(string key, TimeSpan expiry)
        => await _db.KeyExpireAsync(key, expiry);

    public async Task RemoveFromSetAsync(string key, string value)
        => await _db.SetRemoveAsync(key, value);

    public async Task<IReadOnlyCollection<string>> GetSetMembersAsync(string key)
        => [.. (await _db.SetMembersAsync(key)).Select(x => x.ToString())];
}

