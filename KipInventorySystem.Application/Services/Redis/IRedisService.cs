namespace KipInventorySystem.Application.Services.Redis;

public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
    Task AddToSetAsync(string key, string value);
    Task ExpireAsync(string key, TimeSpan expiry);
    Task RemoveFromSetAsync(string key, string value);
    Task<IReadOnlyCollection<string>> GetSetMembersAsync(string key);
}


