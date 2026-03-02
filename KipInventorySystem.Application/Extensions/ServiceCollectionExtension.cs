using KipInventorySystem.Application.Services.Auth;
using KipInventorySystem.Application.Services.Email;
using KipInventorySystem.Application.Validators.Auth;
using KipInventorySystem.Shared.Interfaces;
using KipInventorySystem.Shared.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace KipInventorySystem.Application.Extensions;

public static class ServiceCollectionExtension
{
    public static void AddApplication(this IServiceCollection services, WebApplicationBuilder builder)
    {
        // Register your application services here
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(AuthMapper).Assembly);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailBackgroundJobs, EmailBackgroundJobs>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        services.AddValidatorsFromAssemblyContaining<RegisterDTOValidator>();
        services.AddFluentValidationAutoValidation();

    }
}
