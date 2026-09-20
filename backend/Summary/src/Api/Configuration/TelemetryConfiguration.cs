using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Summary.Api.Configuration;

public static class TelemetryConfiguration
{
    public static WebApplicationBuilder AddSummaryTelemetry(this WebApplicationBuilder builder)
    {
        var samplingRatio = Math.Clamp(
            builder.Configuration.GetValue("Telemetry:SamplingRatio", 1.0),
            0.0,
            1.0);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    builder.Configuration["OTEL_SERVICE_NAME"] ?? "cash-flow-summary-api",
                    serviceVersion: builder.Configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Configuration["OTEL_ENVIRONMENT"]
                        ?? builder.Environment.EnvironmentName,
                    ["service.namespace"] = "cashflow",
                    ["service.instance.id"] = Environment.MachineName
                }))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("CashFlow.Summary.Messaging")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("CashFlow.Summary")
                .AddOtlpExporter());

        return builder;
    }
}
