# Reglas de Audit Logging — Trazabilidad de cambios sensibles

> **Estándar**: SOC 2 CC7.2, ISO 27001 A.12.4 (Logging and monitoring),
> NIST 800-53 AU-2/AU-3/AU-12, OWASP ASVS V7 (Error Handling and Logging).

## 1. Qué hay que auditar (obligatorio)

### Eventos de seguridad
- Login (éxito y fallo) — incluyendo razón del fallo
- Logout (explícito y por expiración)
- Cambio de password / reset de password
- Activación / desactivación de MFA
- Bloqueo de cuenta (5+ intentos fallidos)
- Generación y revocación de refresh tokens
- Step-up MFA (éxito y fallo)

### Eventos de autorización
- Asignación de rol a usuario (`UserRoleAssigned`)
- Remoción de rol de usuario (`UserRoleUnassigned`)
- Concesión de permiso a rol (`PermissionGranted`)
- Revocación de permiso de rol (`PermissionRevoked`)
- Creación / modificación / eliminación de roles
- Creación / modificación / eliminación de permisos
- Acceso denegado (`403 Forbidden`) — solo si el endpoint es sensible

### Eventos de datos sensibles
- Acceso a registros médicos / financieros / PII
- Exportación de datos (CSV, PDF, etc.)
- Cambios masivos (operaciones bulk)
- Acciones del usuario sobre cuentas que no son la suya (`*.on-behalf-of`)

## 2. Formato de log (estructurado)

### `PermissionAuditLog` (tabla append-only en BD)
```csharp
public sealed class PermissionAuditLog : Entity<Guid>
{
    public Guid Id { get; private set; }                  // PK
    public DateTime PerformedAt { get; private set; }      // UTC
    public Guid PerformedBy { get; private set; }          // user que ejecuta
    public string PerformedByEmail { get; private set; }   // denormalizado para queries históricas
    public PermissionAuditAction Action { get; private set; }
    public Guid? TargetUserId { get; private set; }        // a quién se le aplica
    public Guid? TargetRoleId { get; private set; }
    public Guid? TargetPermissionId { get; private set; }
    public string? Reason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }     // traceId del request
    public Guid? TenantId { get; private set; }
    public string? MetadataJson { get; private set; }      // payload adicional
}

public enum PermissionAuditAction
{
    UserRoleAssigned = 1,
    UserRoleUnassigned = 2,
    PermissionGranted = 3,
    PermissionRevoked = 4,
    RoleCreated = 5,
    RoleUpdated = 6,
    RoleDeleted = 7,
    PermissionCreated = 8,
    PermissionUpdated = 9,
    PermissionDeleted = 10,
    UserActivated = 11,
    UserDeactivated = 12,
    AccessDeniedSensitive = 13,
}
```

### Log estructurado (Serilog) para eventos non-DB
```csharp
// Login fallido
_logger.LogWarning("Login failed for {Email} from {IpAddress}: {Reason}",
    email, ipAddress, reason);

// Acceso a recurso sensible
_logger.LogInformation(
    "Sensitive resource accessed: {ResourceType}/{ResourceId} by {UserId} ({Email})",
    "Patient", patientId, currentUser.Id, currentUser.Email);

// Step-up MFA exitoso
_logger.LogInformation(
    "Step-up MFA succeeded for {UserId} on action {Action}",
    userId, action);
```

## 3. Reglas de inmutabilidad

`PermissionAuditLog` es **append-only**:

```sql
-- Migración EF Core: bloquear UPDATE y DELETE a nivel de BD
CREATE TRIGGER tr_PermissionAuditLog_NoUpdate
ON PermissionAuditLog
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 50001, 'PermissionAuditLog is append-only', 1;
END
```

**A nivel de aplicación**:
- El repositorio `IPermissionAuditLogRepository` solo expone `AddAsync` y queries de lectura.
- No existe método `Update` ni `Delete`.
- EF Core: configurar la entidad con `.ToTable(t => t.HasTrigger("tr_PermissionAuditLog_NoUpdate"))`.

## 4. Datos sensibles — qué NO loguear

```csharp
// ❌ NUNCA loguear
- Passwords (ni siquiera hasheados)
- Tokens (JWT completos, refresh tokens)
- Números completos de tarjeta de crédito (PAN)
- SSN / DNI completos
- Datos médicos detallados (diagnóstico, medicación)
- Headers de autorización (Authorization: Bearer ...)

// ✅ Sí loguear (formas seguras)
- Email del usuario (PII pero útil para auditoría — ver retención)
- IP address (con anonimización configurable por GDPR)
- IDs de recursos (Guid no es sensible)
- Hash o últimos 4 dígitos de PAN: "****1234"
- Tipo de operación: "patient_data_accessed" en vez del payload
```

### Serilog — destructuring filter
```csharp
// Program.cs — excluir propiedades sensibles del log
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Destructure.ByTransforming<LoginRequest>(r => new { r.Email })            // omite Password
    .Destructure.ByTransforming<JwtToken>(_ => "***REDACTED***")
    .Filter.ByExcluding(Matching.WithProperty<string>("Authorization", _ => true)));
```

## 5. Retención

| Tipo de log | Retención | Storage |
|---|---|---|
| `PermissionAuditLog` (BD) | **7 años** (cumplimiento financiero/médico) | SQL principal + archivado a Blob después de 1 año |
| Logs de seguridad (Serilog → Seq) | **1 año** caliente, **3 años** archivado | Seq + Azure Blob cold tier |
| Logs de aplicación (info/debug) | **30 días** | Seq |
| Logs de health checks | **7 días** | Seq |

**Regla clave**: borrar PII según GDPR no implica borrar el audit log. Anonimizar el `PerformedByEmail` reemplazándolo con hash o tombstone, pero conservar el `PerformedBy` (Guid).

## 6. Correlación — traceId end-to-end

Todo audit log incluye **`CorrelationId`** (traceId de OpenTelemetry):

```csharp
// Middleware de enriquecimiento
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Items["CorrelationId"] = traceId;
        context.Response.Headers["X-Correlation-Id"] = traceId;
        using (LogContext.PushProperty("CorrelationId", traceId))
            await next(context);
    }
}
```

Esto permite correlacionar:
- Un cambio de permiso en `PermissionAuditLog`
- Con el log HTTP del request en Seq
- Con la traza distribuida en Jaeger/Tempo
- Con las queries EF Core que ejecutó

## 7. Acceso a logs de auditoría

Solo usuarios con permiso `audit.read`:

```csharp
group.MapGet("/api/v1/audit/permissions", GetPermissionAuditAsync)
    .RequirePermission("audit.read")
    .WithTags("Audit");
```

**Reglas**:
- Acceso al log de auditoría **se audita a sí mismo** (`audit.viewed` event en Serilog).
- Filtros obligatorios: rango de fechas (máximo 90 días por query) + paginación.
- Export bulk (CSV) requiere step-up MFA.

## 8. Domain events — emisión obligatoria

Todo cambio sensible debe emitir un domain event. La plantilla provee handlers que escriben al `PermissionAuditLog`:

```csharp
// Domain
role.GrantPermission(permission, currentUser.Id);
// → emite PermissionGranted event

// Application
public sealed class PermissionGrantedHandler(IPermissionAuditLogRepository repo, IHttpContextAccessor http)
    : INotificationHandler<PermissionGranted>
{
    public async Task Handle(PermissionGranted @event, CancellationToken ct)
    {
        await repo.AddAsync(new PermissionAuditLog(
            performedBy: @event.GrantedBy,
            action: PermissionAuditAction.PermissionGranted,
            targetRoleId: @event.RoleId,
            targetPermissionId: @event.PermissionId,
            ipAddress: http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            userAgent: http.HttpContext?.Request.Headers.UserAgent.ToString(),
            correlationId: Activity.Current?.TraceId.ToString()), ct);
    }
}
```

## 9. Alertas (SIEM)

Configurar alertas en Seq/Datadog/Grafana para:

| Patrón | Severidad | Acción |
|---|---|---|
| > 5 logins fallidos del mismo email en 5 min | Alta | Bloqueo automático + notificar al usuario |
| > 10 `403 Forbidden` del mismo userId en 1 min | Alta | Investigar — posible token comprometido |
| Asignación de rol `super-admin` o `tenant-admin` | Crítica | Notificar a todo el equipo de seguridad |
| Acceso a `audit.read` desde IP nueva | Media | Log + revisión semanal |
| `PermissionAuditLog` insert con > 100/min | Media | Posible script de exfiltración |

## 10. Checklist de implementación

- [ ] Tabla `PermissionAuditLog` con trigger anti-update/delete
- [ ] Repositorio expone solo `AddAsync` y queries
- [ ] Domain events para todos los cambios de permisos/roles
- [ ] Handlers de eventos escriben al audit log dentro del mismo `UnitOfWork`
- [ ] Middleware de `CorrelationId` registrado primero en el pipeline
- [ ] Serilog configurado con destructure filter para passwords/tokens
- [ ] Endpoint `/api/v1/audit/permissions` con permiso `audit.read`
- [ ] Política de retención implementada (job de archivado mensual)
- [ ] Alertas SIEM definidas y probadas
- [ ] Tests de integración que verifican que cada operación genera audit log
