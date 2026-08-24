using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FintechBackend.Filters;

public class IdempotencyHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Only apply to POST and PUT endpoints (e.g., Transactions)
        var httpMethod = context.ApiDescription.HttpMethod?.ToUpperInvariant();
        if (httpMethod is "POST" or "PUT")
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Idempotency-Key",
                In = ParameterLocation.Header,
                Required = false, // Set to true if the header is mandatory
                Description = "Unique UUID key to ensure idempotent processing and prevent duplicate executions",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Format = "uuid",
                    Example = new Microsoft.OpenApi.Any.OpenApiString(Guid.NewGuid().ToString())
                }
            });
        }
    }
}