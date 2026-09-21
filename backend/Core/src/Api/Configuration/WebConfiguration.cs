using Microsoft.OpenApi.Models;

namespace Core.Api.Configuration;

public static class WebConfiguration
{
    public static IServiceCollection AddCoreWeb(this IServiceCollection services)
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
                    Title = "CashFlow Core API",
                    Version = "v1",
                    Description = "Lançamentos, auditoria e administração de usuários do CashFlow."
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

            options.OperationFilter<CoreSwaggerExamplesOperationFilter>();
        });

        return services;
    }
}
