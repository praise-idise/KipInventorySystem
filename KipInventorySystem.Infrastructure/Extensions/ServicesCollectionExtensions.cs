using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Services.FilesUpload;
using KipInventorySystem.Application.Services.Inventory.Common;
using KipInventorySystem.Application.Services.Payment;
using KipInventorySystem.Application.Services.Redis;
using KipInventorySystem.Domain.Entities;
using KipInventorySystem.Domain.Interfaces;
using KipInventorySystem.Infrastructure.Integrations.Cloudinary;
using KipInventorySystem.Infrastructure.Integrations.Email;
using KipInventorySystem.Infrastructure.Integrations.Hangfire;
using KipInventorySystem.Infrastructure.Integrations.Redis;
using KipInventorySystem.Infrastructure.Integrations.Stripe;
using KipInventorySystem.Infrastructure.Persistence;
using KipInventorySystem.Infrastructure.Repositories;
using KipInventorySystem.Infrastructure.Services.Inventory;
using KipInventorySystem.Infrastructure.Seeder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.Infrastructure.Extensions;

public static class ServicesCollectionExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection string is missing.")));
        services.AddHealthChecks().AddNpgSql(configuration.GetConnectionString("DefaultConnection")!,
        name: "PostgreSQL",
        failureStatus: HealthStatus.Unhealthy);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IInventoryTransactionRunner, InventoryTransactionRunner>();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
        })
          .AddRoles<IdentityRole>()
          .AddEntityFrameworkStores<ApplicationDbContext>()
          .AddDefaultTokenProviders();

        // 24-hour expiry for email confirmation + password reset tokens
        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        services.AddRedis(configuration);

        services.AddHealthChecks().AddRedis(configuration.GetConnectionString("Redis")!,
        name: "Redis",
        failureStatus: HealthStatus.Unhealthy
        );
        services.AddScoped<IRedisService, RedisService>();

        // Cloudinary
        services.AddOptions<CloudinarySettings>()
        .Bind(configuration.GetSection("Cloudinary"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        services.AddScoped<IFilesUploadService, CloudinaryService>();

        // SMTP Email
        services.AddOptions<SmtpSettings>()
        .Bind(configuration.GetSection("Smtp"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        services.AddScoped<IEmailService, EmailService>();

        // Stripe
        services.AddOptions<StripeSettings>()
        .Bind(configuration.GetSection("Stripe"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        services.AddScoped<IPaymentService, StripeService>();

        // Hangfire
        services.AddOptions<HangfireSettings>()
        .Bind(configuration.GetSection("Hangfire"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

        services.AddHangfire(configuration);

        // Seeders
        services.AddScoped<IApplicationSeeder, ApplicationSeeder>();

    }
}
