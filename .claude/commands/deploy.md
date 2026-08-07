# /project:deploy

Pipeline de deploy para el backend .NET en contenedores.

## Uso
```
/project:deploy <entorno>
```
Entornos: `staging` | `production`

## Pipeline completo

### 1. Pre-deploy checks
```bash
dotnet build -c Release --no-incremental -warnaserror
dotnet test --no-build -c Release --filter "Category!=Integration"
dotnet format --verify-no-changes
```

### 2. Docker build multi-stage
```dockerfile
# Stage 1: Restore (cacheado)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src
COPY *.sln .
COPY **/*.csproj ./
RUN dotnet restore

# Stage 2: Build + Test
FROM restore AS build
COPY . .
RUN dotnet build -c Release --no-restore -warnaserror
RUN dotnet test -c Release --no-build --filter "Category=Unit"

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish src/YourApp.Api/YourApp.Api.csproj \
    -c Release --no-build \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 4: Runtime (imagen mínima)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
RUN adduser --disabled-password --home /app --gecos '' appuser
USER appuser
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "YourApp.Api.dll"]
```

### 3. Docker Compose (staging)
```yaml
version: '3.9'
services:
  api:
    image: yourapp-api:${VERSION}
    environment:
      - ASPNETCORE_ENVIRONMENT=Staging
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION}
      - Redis__ConnectionString=${REDIS_CONNECTION}
    ports: ["8080:8080"]
    depends_on: [sqlserver, redis]
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      retries: 3
```

### 4. Kubernetes (producción)
```yaml
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: yourapp-api
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate: { maxSurge: 1, maxUnavailable: 0 }
  template:
    spec:
      containers:
      - name: api
        image: yourregistry/yourapp-api:${VERSION}
        ports: [{containerPort: 8080}]
        resources:
          requests: {cpu: "100m", memory: "256Mi"}
          limits: {cpu: "500m", memory: "512Mi"}
        livenessProbe:
          httpGet: {path: /health/live, port: 8080}
        readinessProbe:
          httpGet: {path: /health/ready, port: 8080}
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef: {name: app-secrets, key: db-connection}
```

### 5. Migraciones EF Core en deploy
```bash
# Aplicar migraciones antes de iniciar la app (init container en K8s)
dotnet ef database update --project src/YourApp.Infrastructure
```

### 6. Post-deploy verificación
```bash
curl -f https://api.yourapp.com/health/ready
curl -f https://api.yourapp.com/health/live
```
