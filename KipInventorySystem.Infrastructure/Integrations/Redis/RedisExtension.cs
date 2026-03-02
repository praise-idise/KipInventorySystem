using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KipInventorySystem.Infrastructure.Integrations.Redis;

public static class RedisExtension
{
    public static void AddRedis(
       this IServiceCollection services,
       IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is missing.");


        var multiplexer = ConnectionMultiplexer.Connect(connectionString);

        // Log connection events
        multiplexer.ConnectionFailed += (sender, args) =>
        {
            Console.WriteLine($"Redis connection failed: {args.Exception?.Message}");
        };

        multiplexer.ConnectionRestored += (sender, args) =>
        {
            Console.WriteLine("Redis connection restored");
        };


        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    }
}
