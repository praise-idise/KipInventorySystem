using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KipInventorySystem.Infrastructure.Integrations.Hangfire;

public static class HangfireExtension
{
    public static void AddHangfire(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = 3,
            DelaysInSeconds = [60, 300, 900],
            OnAttemptsExceeded = AttemptsExceededAction.Delete
        });

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
        options => options.UseNpgsqlConnection(
            configuration.GetConnectionString("DefaultConnection")!),
        new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseNativeDatabaseTransactions = true
        }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 1;
            options.Queues = ["default", "emails"];
        });
    }
}
