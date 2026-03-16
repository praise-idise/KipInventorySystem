using KipInventorySystem.API.Attributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KipInventorySystem.API.Extensions;

public sealed class RequiresIdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresIdempotencyKey = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<RequiresIdempotencyKeyAttribute>()
            .Any();

        if (!requiresIdempotencyKey)
        {
            return;
        }

        operation.Parameters ??= [];

        if (operation.Parameters.Any(parameter =>
                string.Equals(parameter.Name, "X-Idempotency-Key", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Reuse the same key only when retrying the exact same request.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Example = new Microsoft.OpenApi.Any.OpenApiString("6c2f6d59-9a3e-4f8c-8f3d-3f15ac4a9e6b")
            }
        });
    }
}
