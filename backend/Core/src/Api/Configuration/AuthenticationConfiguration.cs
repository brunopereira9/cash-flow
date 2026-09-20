using System.Text;
using CashFlow.BuildingBlocks.Identity;
using Core.Api.Application.Interfaces;
using Core.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Core.Api.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddCoreAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwt(options, configuration));

        services.AddAuthorization();
        services.AddHttpClient("keycloak-current-state");
        services.AddScoped<KeycloakStateClient>();
        services.AddScoped<ICurrentKeycloakAuthorization, CurrentKeycloakAuthorization>();
        services.AddScoped<IKeycloakAdminClient, KeycloakAdminClient>();

        return services;
    }

    private static void ConfigureJwt(
        JwtBearerOptions options,
        IConfiguration configuration)
    {
        options.MapInboundClaims = false;
        options.Authority = configuration["Keycloak:Authority"];
        options.Audience = configuration["Keycloak:Audience"] ?? "cashflow";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidIssuer =
            configuration["Keycloak:Issuer"] ?? configuration["Keycloak:Authority"];

        var testKey = configuration["Keycloak:ValidationSigningKey"];
        if (string.IsNullOrWhiteSpace(testKey))
            return;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testKey)),
            ValidateIssuer = true,
            ValidIssuer = configuration["Keycloak:Issuer"] ?? configuration["Keycloak:Authority"],
            ValidateAudience = true,
            ValidAudience = configuration["Keycloak:Audience"] ?? "cashflow",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }
}
