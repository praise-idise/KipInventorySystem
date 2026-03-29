using System.Text.Json;
using Asp.Versioning.ApiExplorer;
using KipInventorySystem.API.Extensions;
using KipInventorySystem.Application.Extensions;
using KipInventorySystem.Infrastructure.Extensions;
using KipInventorySystem.Infrastructure.Persistence;
using KipInventorySystem.Infrastructure.Seeder;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;
using SwaggerThemes;
using static KipInventorySystem.Shared.Models.AppSettings;
using KipInventorySystem.API.Middlewares;
using HangfireBasicAuthenticationFilter;

var healthJsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddPresentation();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder);

builder.Services.AddControllers();

var app = builder.Build();

// Only apply automatic schema changes and seed data in development.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetService<IApplicationSeeder>();
    if (seeder is not null)
    {
        await seeder.SeedAsync();
    }
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseRateLimiter();

app.UseSerilogRequestLogging();

app.UseCors("DefaultCorsPolicy");

var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(Theme.UniversalDark, customStyles: null, setupAction: options =>
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}
;

app.UseHttpsRedirection();
app.UseAuthentication();

app.UseAuthorization();

var hangfireSettings = app.Services.GetRequiredService<IOptions<HangfireSettings>>().Value;

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireCustomBasicAuthenticationFilter
    {
        User = hangfireSettings.DashboardUsername,
        Pass = hangfireSettings.DashboardPassword
    }],
    IgnoreAntiforgeryToken = true
});

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => WriteHealthCheckResponse(context, report, healthJsonOptions)
});

app.Run();

static Task WriteHealthCheckResponse(
    HttpContext context,
    HealthReport report,
    JsonSerializerOptions healthJsonOptions)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        timestamp = DateTime.UtcNow
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, healthJsonOptions));
}
