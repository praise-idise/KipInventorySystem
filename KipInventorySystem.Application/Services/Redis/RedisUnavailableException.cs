namespace KipInventorySystem.Application.Services.Redis;

public sealed class RedisUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
