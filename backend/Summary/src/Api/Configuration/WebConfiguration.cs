using Microsoft.OpenApi.Models;

namespace Summary.Api.Configuration;

public static class WebConfiguration
{
    public static IServiceCollection AddSummaryWeb(this IServiceCollection services)
    {
        services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "CashFlow Summary API",
                    Version = "v1",
                    Description = "Projeções e resumos diários do CashFlow."
                });

            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Informe apenas o token JWT."
                });

            options.OperationFilter<SummarySwaggerExamplesOperationFilter>();
        });

        return services;
    }
}
