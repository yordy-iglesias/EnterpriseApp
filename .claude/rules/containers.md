# Reglas de Contenedores y Despliegue — .NET 9

## Dockerfile estándar (multi-stage optimizado)
```dockerfile
# escape=`
ARG DOTNET_VERSION=9.0

# ─── Stage 1: Base runtime ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS base
RUN apk add --no-cache curl  # para healthcheck
WORKDIR /app
EXPOSE 8080

# ─── Stage 2: Restore (cacheado mientras no cambien .csproj) ─────────────────
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS restore
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["YourApp.sln", "."]
COPY ["src/YourApp.Api/YourApp.Api.csproj", "src/YourApp.Api/"]
COPY ["src/YourApp.Application/YourApp.Application.csproj", "src/YourApp.Application/"]
COPY ["src/YourApp.Domain/YourApp.Domain.csproj", "src/YourApp.Domain/"]
COPY ["src/YourApp.Infrastructure/YourApp.Infrastructure.csproj", "src/YourApp.Infrastructure/"]
RUN dotnet restore --locked-mode

# ─── Stage 3: Build ───────────────────────────────────────────────────────────
FROM restore AS build
COPY . .
RUN dotnet build -c Release --no-restore -warnaserror

# ─── Stage 4: Test ───────────────────────────────────────────────────────────
FROM build AS test
RUN dotnet test -c Release --no-build \
    --filter "Category=Unit" \
    --logger "trx;LogFileName=test-results.trx" \
    /p:CollectCoverage=true /p:CoverageOutputFormat=cobertura

# ─── Stage 5: Publish ────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish src/YourApp.Api/YourApp.Api.csproj \
    -c Release --no-build \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false

# ─── Stage 6: Final (imagen mínima) ─────────────────────────────────────────
FROM base AS final
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser
WORKDIR /app
COPY --from=publish /app/publish .

# Variables de entorno por defecto
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCConserveMemory=9

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "YourApp.Api.dll"]
```

## Docker Compose — desarrollo local
```yaml
# docker-compose.dev.yml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: Y
      MSSQL_SA_PASSWORD: "YourPass123!"
    ports: ["1433:1433"]
    volumes: [sqldata:/var/opt/mssql]

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
    command: redis-server --save 60 1 --loglevel warning

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest

  seq:
    image: datalust/seq:latest
    environment: { ACCEPT_EULA: Y }
    ports: ["5341:80"]

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # UI
      - "4317:4317"    # OTLP gRPC
      - "4318:4318"    # OTLP HTTP

volumes:
  sqldata:
```

## .NET Aspire AppHost
```csharp
// YourApp.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");
var sql = builder.AddSqlServer("sql")
    .AddDatabase("yourapp-db");
var rabbit = builder.AddRabbitMQ("messaging");

var api = builder.AddProject<Projects.YourApp_Api>("api")
    .WithReference(redis)
    .WithReference(sql)
    .WithReference(rabbit)
    .WithHttpHealthCheck("/health/ready");

builder.AddProject<Projects.YourApp_Worker>("worker")
    .WithReference(sql)
    .WithReference(rabbit);

builder.Build().Run();
```

## Helm chart structure (producción)
```
charts/yourapp/
├── Chart.yaml
├── values.yaml          # valores por defecto
├── values.prod.yaml     # overrides de producción
├── templates/
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   ├── configmap.yaml
│   ├── secret.yaml      # referencias a Azure Key Vault
│   ├── hpa.yaml         # Horizontal Pod Autoscaler
│   └── pdb.yaml         # Pod Disruption Budget
```

## Seguridad en contenedores
- Imagen base Alpine (mínima superficie de ataque)
- Usuario no-root obligatorio
- `--read-only` filesystem donde sea posible
- Sin secrets en variables de entorno en producción (usar K8s Secrets + CSI Driver)
- Escaneo de vulnerabilidades con `docker scout` o Trivy en CI
- Límites de CPU y memoria obligatorios en K8s
