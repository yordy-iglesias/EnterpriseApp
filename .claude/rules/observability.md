# Reglas de Observabilidad — .NET 9 + .NET Aspire

## Los tres pilares: Logs, Métricas, Trazas

### Setup con .NET Aspire (ServiceDefaults)
```csharp
// YourApp.ServiceDefaults/Extensions.cs
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();   // Trazas + métricas automáticas
    builder.AddDefaultHealthChecks();   // Health checks estándar
    builder.Services.AddServiceDiscovery();
    return builder;
}

public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter())
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddRedisInstrumentation()
            .AddSource(DiagnosticsConfig.ActivitySourceName)
            .AddOtlpExporter()); // Jaeger / Zipkin / Tempo
    return builder;
}
```

## Serilog — Structured Logging
```csharp
// Program.cs
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "YourApp.Api")
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.Seq(context.Configuration["Serilog:SeqUrl"]!)
    .WriteTo.File("logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7));
```

### Niveles de log
- **Debug**: información detallada de desarrollo (desactivado en producción)
- **Information**: flujo normal de la app, inicio/fin de operaciones
- **Warning**: situaciones inesperadas pero manejadas (retry, fallback)
- **Error**: errores que requieren atención (excepciones no manejadas)
- **Fatal**: la aplicación no puede continuar

### Propiedades obligatorias en logs
```csharp
// Usar structured logging — NUNCA string interpolation
_logger.LogInformation("User {UserId} created product {ProductId}", userId, productId);
// ❌ Nunca: $"User {userId} created product {productId}"
```

## Health Checks
```csharp
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"])
    .AddRedis(builder.Configuration["Redis:ConnectionString"]!, "redis", tags: ["ready"])
    .AddRabbitMQ(tags: ["ready"])
    .AddCheck("memory", () =>
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        return allocated < 500_000_000L // 500MB
            ? HealthCheckResult.Healthy($"Memory: {allocated / 1_048_576}MB")
            : HealthCheckResult.Degraded($"High memory: {allocated / 1_048_576}MB");
    }, tags: ["live"]);

// Exponer endpoints diferenciados
app.MapHealthChecks("/health/live", new() { Predicate = c => c.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health", new() { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse });
```

## Métricas personalizadas
```csharp
public static class AppMetrics
{
    private static readonly Meter Meter = new("YourApp.Api", "1.0.0");

    public static readonly Counter<long> OrdersCreated =
        Meter.CreateCounter<long>("orders.created", "orders", "Total orders created");

    public static readonly Histogram<double> OrderProcessingTime =
        Meter.CreateHistogram<double>("orders.processing_time", "ms", "Order processing time");

    // Uso en handler
    AppMetrics.OrdersCreated.Add(1, new TagList { { "status", "success" } });
}
```

## Distributed Tracing
```csharp
public static class DiagnosticsConfig
{
    public const string ActivitySourceName = "YourApp";
    public static readonly ActivitySource Source = new(ActivitySourceName);
}

// Uso en servicios
using var activity = DiagnosticsConfig.Source.StartActivity("ProcessOrder");
activity?.SetTag("order.id", orderId);
activity?.SetTag("order.amount", amount);
```

## Alertas recomendadas (Prometheus/Grafana)
| Métrica | Condición de alerta |
|---|---|
| Error rate | > 1% de requests en 5min |
| P99 latency | > 2 segundos |
| Memory usage | > 80% del límite del contenedor |
| DB connection pool | > 90% de conexiones usadas |
| Failed health checks | 2 consecutivos |
