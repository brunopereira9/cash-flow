using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Summary.Api;

public sealed class SummarySwaggerExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.Split('?')[0];
        if (path is null) return;
        operation.Tags ??=
        [
            new OpenApiTag
            {
                Name = path.StartsWith("health") || path.StartsWith("ready") ? "Health" :
                    path.StartsWith("internal") ? "Internal" : "Summary"
            }
        ];
        if (path == "healthz" && operation.Responses.TryGetValue("200", out var healthResponse) &&
            healthResponse.Content.TryGetValue("application/json", out var healthContent))
            healthContent.Examples["default"] = new OpenApiExample
            {
                Value = new OpenApiObject
                    { ["status"] = new OpenApiString("ok"), ["service"] = new OpenApiString("summary") }
            };
        if (path == "summary/daily/{date}")
        {
            var dateParameter = operation.Parameters.FirstOrDefault(p => p.Name == "date");
            if (dateParameter is not null) dateParameter.Example = new OpenApiString("2026-09-20");
        }
    }
}