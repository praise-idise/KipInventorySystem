using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Services.FilesUpload;
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
        failureStatus: HealthStatus.Degraded);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
        })
          .AddRoles<IdentityRole>()
          .AddEntityFrameworkStores<ApplicationDbContext>()
          .AddDefaultTokenProviders();

        services.AddRedis(configuration);

        services.AddHealthChecks().AddRedis(configuration.GetConnectionString("Redis")!,
        name: "Redis",
        failureStatus: HealthStatus.Degraded
        );
        services.AddScoped<IRedisService, RedisService>();

        // Cloudinary
        services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
        services.AddScoped<IFilesUploadService, CloudinaryService>();

        // SMTP Email
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailService, EmailService>();

        // Stripe
        services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
        services.AddScoped<IPaymentService, StripeService>();

        // Hangfire
        services.Configure<HangfireSettings>(configuration.GetSection("Hangfire"));
        services.AddHangfire(configuration);

        // Seeders
        services.AddScoped<IApplicationSeeder, ApplicationSeeder>();

    }
}
