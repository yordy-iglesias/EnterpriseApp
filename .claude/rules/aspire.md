# Reglas de .NET Aspire — Orquestación y Observabilidad

## ¿Qué es .NET Aspire?
Conjunto de herramientas y convenciones para crear aplicaciones distribuidas cloud-ready.
Proporciona: orquestación de servicios, integración de componentes, observabilidad y dashboards.

## Estructura de proyectos Aspire
```
YourApp.sln
├── src/
│   ├── YourApp.AppHost/           # Proyecto orquestador (solo dev/staging)
│   ├── YourApp.ServiceDefaults/   # Extensiones compartidas (OTel, health, etc.)
│   ├── YourApp.Api/               # API principal
│   ├── YourApp.Worker/            # Background workers
│   └── YourApp.MigrationService/  # Servicio de migraciones DB
```

## AppHost — Orquestación completa
```csharp
// YourApp.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Infraestructura
var cache = builder.AddRedis("cache")
    .WithRedisInsight()   // UI de Redis en desarrollo
    .WithPersistence();

var sql = builder.AddSqlServer("sql")
    .WithSqlServerManagement()  // SQL Server Management en dev
    .AddDatabase("yourapp-db");

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();  // UI de RabbitMQ

var seq = builder.AddSeq("seq");  // Logging

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();  // Azurite en dev

// Servicios de aplicación
var migrationService = builder.AddProject<Projects.YourApp_MigrationService>("migrations")
    .WithReference(sql)
    .WaitFor(sql);

var api = builder.AddProject<Projects.YourApp_Api>("api")
    .WithReference(cache)
    .WithReference(sql)
    .WithReference(messaging)
    .WithReference(seq)
    .WaitForCompletion(migrationService)  // Espera migraciones
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints();  // Expone endpoint fuera de Aspire

var worker = builder.AddProject<Projects.YourApp_Worker>("worker")
    .WithReference(sql)
    .WithReference(messaging)
    .WithReference(cache)
    .WaitForCompletion(migrationService);

builder.Build().Run();
```

## ServiceDefaults — Configuración compartida
```csharp
// YourApp.ServiceDefaults/Extensions.cs
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    // OpenTelemetry
    builder.ConfigureOpenTelemetry();

    // Health checks básicos
    builder.AddDefaultHealthChecks();

    // Service Discovery (Aspire resolución de nombres)
    builder.Services.AddServiceDiscovery();

    // HttpClient con resilencia y service discovery
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();  // Retry + circuit breaker
        http.AddServiceDiscovery();
    });

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
            .AddRuntimeInstrumentation())
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation());

    builder.AddOpenTelemetryExporters();
    return builder;
}
```

## Integraciones Aspire disponibles
```csharp
// En el proyecto que consume el recurso:
builder.AddRedisClient("cache");               // IConnectionMultiplexer
builder.AddRedisDistributedCache("cache");     // IDistributedCache
builder.AddRedisOutputCache("cache");          // Output Cache
builder.AddSqlServerDbContext<AppDbContext>("yourapp-db");  // DbContext
builder.AddRabbitMQClient("messaging");        // IConnection
builder.AddAzureBlobClient("storage");         // BlobServiceClient
```

## Dashboard de Aspire
Al ejecutar el AppHost, el dashboard se expone en `https://localhost:15888`:
- **Resources**: estado de todos los servicios y contenedores
- **Logs estructurados**: filtrado en tiempo real
- **Trazas distribuidas**: visualización de spans entre servicios
- **Métricas**: gráficos de CPU, memoria, requests

## Producción sin AppHost
En producción, el AppHost no se usa. Los servicios se configuran mediante:
- Variables de entorno para connection strings (resueltas por Aspire conventions)
- Kubernetes secrets / Azure Key Vault
- El código de los proyectos de servicio es el mismo — solo cambia la infraestructura

## Convención de nombres de recursos
```csharp
// Los nombres en AddRedis("cache"), AddSqlServer("sql"), etc.
// se mapean a variables de entorno automáticamente:
// ConnectionStrings__cache, ConnectionStrings__sql
// Esto permite usar el mismo código en dev (Aspire) y prod (K8s secrets)
```
