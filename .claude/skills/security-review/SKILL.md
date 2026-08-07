# SKILL: Security Review — Auto-invocado

## Trigger
Se activa cuando el usuario dice:
- "revisa seguridad de…"
- "haz un security review"
- "audita vulnerabilidades"
- "analiza OWASP / JWT / autorización"
- Antes de cualquier deploy a producción (pre-deploy hook)
- Al introducir dependencias NuGet nuevas

## Objetivo
Revisar el código .NET contra OWASP Top 10, reglas de `rules/security.md`
y vulnerabilidades de dependencias NuGet. Producir un reporte accionable.

## Pasos del workflow

### 1. Escaneo de dependencias
```bash
# Vulnerabilidades en NuGet
dotnet list package --vulnerable --include-transitive

# Paquetes obsoletos
dotnet list package --outdated

# Verificar lock file
dotnet restore --locked-mode
```
- **Bloquear** si hay High/Critical.
- Reportar Medium con plan de upgrade.

### 2. Análisis estático
```bash
# Análisis de seguridad con security-scan
dotnet tool install -g security-scan
security-scan YourApp.sln

# Roslyn analyzers de seguridad
dotnet build -warnaserror /p:AnalysisMode=All

# Detección de secretos commiteados
git log --all -p | grep -E "(password|secret|api[_-]?key|token)\s*[=:]" -i
```

### 3. Checklist de revisión

#### Autenticación (A07 OWASP)
- [ ] JWT: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` = true
- [ ] `ClockSkew` definido y razonable (≤ 1 min)
- [ ] Signing key con longitud mínima (256 bits HMAC)
- [ ] Refresh token rotation activo
- [ ] Hash de password: **BCrypt / Argon2** (nunca MD5/SHA1/SHA256 sin salt+KDF)
- [ ] Rate limiting en `/login`, `/register`, `/forgot-password`
- [ ] Lockout tras N intentos fallidos (configurable)

#### Autorización (A01 OWASP)
- [ ] `FallbackPolicy` requiere autenticación por defecto
- [ ] Políticas específicas (`RequireRole`, `RequireClaim`) en acciones críticas
- [ ] Resource-based authorization para recursos multi-tenant
- [ ] Sin `[AllowAnonymous]` accidental en endpoints privados
- [ ] Validación de ownership: `order.UserId == currentUser.Id`

#### Inyección (A03 OWASP)
- [ ] EF Core: queries parametrizadas (no string interpolation en `FromSqlRaw`)
- [ ] Dapper: parámetros `@param` — nunca concatenación
- [ ] Sin `Process.Start` con input de usuario
- [ ] Sin deserialización de tipos controlados por usuario (`TypeNameHandling.All`)
- [ ] LINQ Dynamic / expression trees: sanitizados

#### Validación (A04 OWASP)
- [ ] FluentValidation en **todos** los Commands y Queries
- [ ] `AddFluentValidationAutoValidation()` registrado
- [ ] Middleware de validación global que devuelve 400 Problem Details
- [ ] Sin `[FromBody]` sin validación
- [ ] Límites de tamaño de request configurados

#### Criptografía (A02 OWASP)
- [ ] HTTPS forzado con HSTS
- [ ] TLS 1.2+ (deshabilitar TLS 1.0/1.1 en Kestrel)
- [ ] Data Protection keys persistidos (Azure Key Vault / Redis)
- [ ] `RandomNumberGenerator` para tokens (no `Random`)
- [ ] Sin claves hardcoded en código o appsettings.json

#### Configuración (A05 OWASP)
- [ ] Secrets en Azure Key Vault / User Secrets — nunca en appsettings.json
- [ ] Headers de seguridad: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`
- [ ] CORS restrictivo — sin `AllowAnyOrigin` + `AllowCredentials`
- [ ] `DeveloperExceptionPage` solo en Development
- [ ] `ProblemDetails` genérico en producción (sin stack traces)

#### Logging y monitoreo (A09 OWASP)
- [ ] Structured logging con Serilog
- [ ] Sin PII en logs: passwords, tokens, PAN de tarjetas, SSNs, emails completos
- [ ] Auditoría de eventos de seguridad: login exitoso/fallido, cambios de rol, accesos denegados
- [ ] Alertas configuradas: > N logins fallidos, accesos denegados repetidos

#### APIs y datos sensibles
- [ ] Sin campos sensibles en responses (PasswordHash, internal IDs)
- [ ] Rate limiting global + por endpoint crítico
- [ ] `[HttpPatch]` con JSON Patch restringe campos mutables
- [ ] Versionado de API: deprecated versions con fecha de end-of-life

### 4. Pruebas manuales recomendadas
- **SQLi**: `' OR 1=1--` en todos los inputs textuales
- **Auth bypass**: modificar JWT `role` claim y enviar
- **IDOR**: `GET /api/v1/orders/{id}` con id de otro usuario
- **Rate limiting**: 1000 req/min a `/login` — verificar lockout
- **Error info leak**: forzar 500 — no debe exponer stack trace
- **CORS**: petición desde origen no permitido — debe rechazarse

### 5. Reporte final

```markdown
## Security Review — {fecha} — {branch}

### 🔴 Crítico (bloqueante para deploy)
- **[archivo:línea]** Descripción
  - **OWASP**: A{número} — {nombre}
  - **CWE**: CWE-{número}
  - **Riesgo**: escenario concreto de explotación
  - **Fix**:
    ```csharp
    // Código corregido
    ```

### 🟠 Alto
(mismo formato)

### 🟡 Medio / 🟢 Bajo
(mismo formato)

### Dependencias NuGet vulnerables
| Paquete | Versión | CVE | Severidad | Fix |
|---|---|---|---|---|
| Microsoft.Data.SqlClient | 5.0.0 | CVE-2024-... | High | Upgrade a 5.2.1 |

### Secretos detectados
<ninguno o listado con redacción>

### Resumen
- X críticos, Y altos, Z medios, W bajos
- Estado: ❌ Bloqueado / ⚠️ Aceptable con plan / ✅ Aprobado
```

## Criterios de aprobación
- **0 críticos** y **0 altos** sin plan de mitigación documentado
- `dotnet list package --vulnerable` sin High/Critical
- Checklist OWASP completado al 100%
- Tests de seguridad (authentication/authorization) pasando
