# SKILL: Deploy Backend — Auto-invocado

## Trigger
- "despliega el backend"
- "construye la imagen de la API"
- "publica en staging/producción"

## Pasos del workflow

### 1. Validación pre-deploy
```bash
dotnet build -c Release -warnaserror --no-incremental
dotnet test -c Release --no-build --filter "Category=Unit"
dotnet format --verify-no-changes
dotnet list package --vulnerable --highest-patch  # Check CVEs
```

### 2. Versioning semántico
Determinar versión: Major.Minor.Patch basado en cambios desde último tag git.
```bash
git describe --tags --abbrev=0  # último tag
git log {last-tag}..HEAD --oneline  # cambios
```

### 3. Build imagen Docker
```bash
docker build \
  --target final \
  --build-arg DOTNET_VERSION=9.0 \
  --label "git.commit=$(git rev-parse HEAD)" \
  --label "build.date=$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  --tag ${REGISTRY}/${APP_NAME}:${VERSION} \
  --tag ${REGISTRY}/${APP_NAME}:latest \
  .
```

### 4. Scan de vulnerabilidades
```bash
docker scout cves ${REGISTRY}/${APP_NAME}:${VERSION}
# Fallar si hay CVEs críticos
```

### 5. Test de smoke de la imagen
```bash
docker run --rm -d -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Staging \
  -e ConnectionStrings__DefaultConnection="${TEST_DB}" \
  --name smoke-test ${REGISTRY}/${APP_NAME}:${VERSION}
sleep 10
curl -f http://localhost:8080/health/ready || (docker stop smoke-test && exit 1)
docker stop smoke-test
```

### 6. Push al registry
```bash
docker push ${REGISTRY}/${APP_NAME}:${VERSION}
docker push ${REGISTRY}/${APP_NAME}:latest
```

### 7. Deploy
Para staging: `docker-compose up -d`
Para producción: `helm upgrade --install yourapp ./charts/yourapp --values values.prod.yaml`

### 8. Aplicar migraciones pendientes
```bash
dotnet ef database update \
  --project src/YourApp.Infrastructure \
  --startup-project src/YourApp.MigrationService \
  --connection "${PROD_DB_CONNECTION}"
```

### 9. Verificar post-deploy
```bash
for i in 1 2 3; do
  sleep 15
  curl -sf https://api.yourapp.com/health/ready && echo "✅ Ready" && break
  echo "⏳ Waiting for service..."
done
```
