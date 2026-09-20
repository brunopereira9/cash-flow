using System.Security.Claims;
using System.Text.Json;
using CashFlow.BuildingBlocks.Identity;
using Microsoft.AspNetCore.Authentication;

namespace Summary.Api.Infrastructure.Identity;

public sealed class CurrentKeycloakAuthorization(
    KeycloakStateClient stateClient)
    : ICurrentKeycloakAuthorization
{
    public async Task<CurrentKeycloakState> ConfirmAsync(ClaimsPrincipal principal, CancellationToken requestToken)
    {
        CashFlowSummaryTelemetry.KeycloakChecks.Add(1);
        var state = await stateClient.GetAsync(principal, requestToken);
        return new CurrentKeycloakState(state.Enabled, state.Roles);
    }
}

public static class CurrentKeycloakAuthorizationApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCurrentKeycloakAuthorization(this IApplicationBuilder app,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("Keycloak:Enabled", false)) return app;
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/summary"))
            {
                await next();
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }

            try
            {
                var state = await context.RequestServices.GetRequiredService<ICurrentKeycloakAuthorization>()
                    .ConfirmAsync(context.User, context.RequestAborted);
                if (!state.Enabled || state.Roles.Count(role => role is "admin" or "operator" or "auditor") != 1)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next();
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                                  or InvalidOperationException or JsonException)
            {
                CashFlowSummaryTelemetry.KeycloakFailures.Add(1);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        });
    }
}
