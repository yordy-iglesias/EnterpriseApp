using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extensiones compartidas por todos los servicios de la solución.
/// Configuran OpenTelemetry, health checks básicos, service discovery y resiliencia HTTP.
/// Generado por la plantilla dotnet-clean-architecture-base.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Agrega las configuraciones por defecto de Aspire al host.
    /// Llamar desde Program.cs antes de AddApplicationServices().
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retry + circuit-breaker automáticos en HttpClient
            http.AddStandardResilienceHandler();
            // Resolución de nombres via Aspire (ej: "https+http://api")
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configura OpenTelemetry: métricas, trazas y logs estructurados.
    /// El exporter OTLP se activa automáticamente cuando Aspire inyecta
    /// OTEL_EXPORTER_OTLP_ENDPOINT; en local sin Aspire no exporta nada.
    /// </summary>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes           = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();
            });

        // Exportar al Aspire Dashboard (o cualquier backend OTLP) si está configurado
        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Aspire inyecta OTEL_EXPORTER_OTLP_ENDPOINT automáticamente al correr via AppHost
        var useOtlp = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlp)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    /// <summary>
    /// Agrega el health check de liveness básico (self-check).
    /// Los health checks de infraestructura (Postgres, Redis) se agregan en el API.
    /// </summary>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        return builder;
    }

    /// <summary>
    /// Mapea los endpoints de health check estándar de Aspire:
    /// - /health  → todos los checks (readiness)
    /// - /alive   → solo liveness (el proceso está vivo)
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health").AllowAnonymous();

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        }).AllowAnonymous();

        return app;
    }
}
