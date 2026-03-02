using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using KipInventorySystem.API.Middlewares;
using KipInventorySystem.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using static KipInventorySystem.Shared.Models.AppSettings;

namespace KipInventorySystem.API.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static void AddPresentation(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
                 (context, configuration) =>
                 {
                     configuration
                         .ReadFrom.Configuration(context.Configuration)
                         .Enrich.FromLogContext()
                         .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)
                         .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                         .WriteTo.Console();
                 });

        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
        builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("Frontend"));
        builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("AdminSettings"));
        builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));

        var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings configuration is missing.");

        var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

        builder.Services.AddAuthentication(options =>
          {
              options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
              options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
          })
          .AddJwtBearer(options =>
          {
              options.TokenValidationParameters = new TokenValidationParameters
              {
                  ValidateIssuer = true,
                  ValidateAudience = true,
                  ValidateLifetime = true,
                  ClockSkew = TimeSpan.Zero,
                  ValidateIssuerSigningKey = true,
                  ValidIssuer = jwtSettings.Issuer,
                  ValidAudience = jwtSettings.Audience,
                  IssuerSigningKey = new SymmetricSecurityKey(key),
              };

              options.Events = new JwtBearerEvents
              {
                  OnTokenValidated = async context =>
                  {
                      var userManager = context.HttpContext
                          .RequestServices.GetRequiredService<UserManager<ApplicationUser>>();

                      var userId = context.Principal!
                          .FindFirstValue(ClaimTypes.NameIdentifier);

                      var tokenVersionClaim = context.Principal!
                          .FindFirst("token_version")?.Value;

                      if (userId == null || tokenVersionClaim == null)
                      {
                          context.Fail("Invalid token");
                          return;
                      }

                      var user = await userManager.FindByIdAsync(userId);

                      if (user == null || user.TokenVersion.ToString() != tokenVersionClaim)
                      {
                          context.Fail("Token revoked");
                      }
                  }
              };
          });

        builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddEndpointsApiExplorer()
        .AddApiVersioning(setup =>
        {
            setup.DefaultApiVersion = new ApiVersion(1, 0);
            setup.AssumeDefaultVersionWhenUnspecified = true;
            setup.ReportApiVersions = true;
        })
        .AddApiExplorer(setup =>
        {
            setup.GroupNameFormat = "'v'VVV";
            setup.SubstituteApiVersionInUrl = true;
        });

        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Application API",
                Version = "v1",
                Description = "API for your application"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Enter your JWT token below. Example: Bearer abcdef12345",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new List<string>()
                }
            });
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));
        });

        builder.Services.AddAuthorization();

        // Custom authorization result handler for consistent JSON error responses
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler,
            CustomAuthorizationMiddlewareResultHandler>();

        // Global exception handling middleware
        builder.Services.AddTransient<ErrorHandlingMiddleware>();

        // CORS Configuration
        var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsSettings>()
            ?? new CorsSettings();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("DefaultCorsPolicy", policy =>
            {
                policy.WithOrigins(corsSettings.AllowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader();

                if (corsSettings.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
            });
        });

    }
}