using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Core.Api;

public sealed class CoreSwaggerExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.Split('?')[0];
        if (path is null) return;
        operation.Tags ??=
        [
            new OpenApiTag
            {
                Name = path.StartsWith("identity") ? "Identity" :
                    path.StartsWith("audit") ? "Audit" :
                    path.StartsWith("health") || path.StartsWith("ready") ? "Health" : "Ledger"
            }
        ];
        if (path == "ledger/entries" && context.ApiDescription.HttpMethod == "POST" &&
            operation.RequestBody?.Content.TryGetValue("application/json", out var requestContent) == true)
            requestContent.Examples["default"] = new OpenApiExample
            {
                Summary = "Novo lançamento",
                Value = new OpenApiObject
                {
                    ["amount"] = new OpenApiDouble(149.90),
                    ["type"] = new OpenApiString("expense"),
                    ["description"] = new OpenApiString("Compra de materiais"),
                    ["businessDate"] = new OpenApiString("2026-09-20")
                }
            };
        if (path == "ledger/entries" && context.ApiDescription.HttpMethod == "GET" &&
            operation.Responses.TryGetValue("200", out var response) &&
            response.Content.TryGetValue("application/json", out var responseContent))
            responseContent.Examples["default"] = new OpenApiExample
            {
                Value = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiString("7d9f3e1a-1b4f-4d2f-9a0b-123456789abc"),
                        ["amount"] = new OpenApiDouble(149.90), ["type"] = new OpenApiString("expense"),
                        ["description"] = new OpenApiString("Compra de materiais"),
                        ["businessDate"] = new OpenApiString("2026-09-20"), ["version"] = new OpenApiInteger(1),
                        ["deleted"] = new OpenApiBoolean(false)
                    }
                }
            };
        if (path == "healthz" && operation.Responses.TryGetValue("200", out var healthResponse) &&
            healthResponse.Content.TryGetValue("application/json", out var healthContent))
            healthContent.Examples["default"] = new OpenApiExample
            {
                Value = new OpenApiObject
                { ["status"] = new OpenApiString("ok"), ["service"] = new OpenApiString("core") }
            };
    }
}