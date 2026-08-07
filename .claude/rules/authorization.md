# Reglas de Autorización — RBAC + Permissions (DB-driven)

> **Estándar de cumplimiento**: OWASP ASVS V4 (Access Control), NIST 800-53 AC-2/AC-3/AC-6,
> ISO 27001 A.9 (Access Control), SOC 2 CC6.1–CC6.3.

## 1. Modelo conceptual

La plantilla implementa un modelo **RBAC jerárquico con permisos atómicos almacenados en BD**,
con ganchos para **ABAC** (autorización basada en atributos del recurso).

```
User ──< UserRole >── Role ──< RolePermission >── Permission ── Module
                       │
                       └── ParentRoleId (jerarquía opcional)
```

### Decisiones arquitectónicas (ADR resumido)

| Decisión | Por qué |
|---|---|
| Permisos en **BD**, no en `enum` hardcoded | Permite añadir/revocar sin redeploy. Soluciona el problema de `ModuleEnum.cs` con 70+ entradas en NHCS. |
| Código de permiso `{module}.{action}[.{qualifier}]` (string) | Legible en logs y JWT. Ejemplo: `patients.view`, `prescriptions.sign`, `invoices.export.pdf`. |
| JWT lleva permisos **resueltos** (no solo roles) | Evita query de permisos en cada request. Política: token corto (≤15 min) + refresh. |
| `Role.PermissionsSnapshot` (string, JWT firmado) | **Hereda y mejora** el patrón de NHCS de guardar el JWT de permisos en `IdentityRole.ConcurrencyStamp`. Aquí lo movemos a un campo dedicado y semánticamente correcto. |
| `UserPermissionsChanged` domain event invalida Redis | Cambio de rol/permiso → evicción inmediata del cache de ese usuario. |
| Append-only audit log (`PermissionAuditLog`) | Trazabilidad para SOC 2 / ISO 27001. Inmutable: solo INSERT. |
| `Permission.IsSensitive` flag | Permisos sensibles requieren MFA reciente o aprobación dual (ABAC). |
| `TenantId?` en `Role` y `User` | Multi-tenant ready. `null` = permiso global del sistema. |

## 2. Naming convention de permisos

```
{module}.{action}[.{qualifier}]
```

**Reglas**:
- Todo en **kebab-case** dentro de cada segmento, separado por `.`
- `module` en singular: `patient`, `prescription`, `invoice`, `user`, `role`
- `action` verbos estándar: `view`, `create`, `update`, `delete`, `export`, `approve`, `sign`, `cancel`
- `qualifier` opcional para variantes: `.own` (recurso propio), `.tenant` (mismo tenant), `.pdf`, `.bulk`

**Ejemplos válidos**:
```
patient.view                  # ver cualquier paciente
patient.view.own              # ver solo pacientes asignados al usuario
prescription.sign             # firmar receta (sensible — requiere MFA)
invoice.export.pdf            # exportar factura a PDF
role.assign                   # asignar rol a otro usuario
audit.read                    # leer logs de auditoría (admin)
```

**Anti-patterns** (no hacer):
```
❌ ViewPatient                # PascalCase, sin módulo
❌ Ver_Paciente               # mezcla idiomas, snake_case
❌ patient_view               # snake_case
❌ patients.full_access       # action genérica — desglosa en permisos atómicos
```

## 3. Entidades de dominio

```csharp
// Domain/Authorization/Permission.cs
public sealed class Permission : Entity<Guid>
{
    public string Code { get; private set; }            // "patient.view"
    public string Module { get; private set; }          // "patient"
    public string Action { get; private set; }          // "view"
    public string DisplayName { get; private set; }     // "Ver pacientes"
    public string? Description { get; private set; }
    public bool IsSensitive { get; private set; }       // requiere MFA reciente
    public bool IsSystem { get; private set; }          // no se puede borrar
    public Guid? TenantId { get; private set; }         // null = global

    private Permission() { }                            // EF
    public static Permission Create(string code, string displayName, bool isSensitive = false) { /* validaciones */ }
}

// Domain/Authorization/Role.cs
public sealed class Role : IdentityRole<Guid>          // hereda de Identity
{
    public Guid? TenantId { get; private set; }
    public Guid? ParentRoleId { get; private set; }    // jerarquía opcional
    public bool IsSystem { get; private set; }
    public string? Description { get; private set; }

    /// <summary>
    /// Snapshot firmado (JWT) con los permisos resueltos del rol.
    /// Equivalente al patrón legacy `ConcurrencyStamp` de NHCS, pero en
    /// un campo dedicado. Se regenera al modificar permisos del rol.
    /// </summary>
    public string? PermissionsSnapshot { get; private set; }
    public DateTime? PermissionsSnapshotUpdatedAt { get; private set; }

    private readonly List<RolePermission> _rolePermissions = new();
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    public void GrantPermission(Permission permission, Guid grantedBy) { /* + domain event */ }
    public void RevokePermission(Guid permissionId, Guid revokedBy) { /* + domain event */ }
    public void RegeneratePermissionsSnapshot(string signedJwt) { /* actualiza snapshot */ }
}

// Domain/Authorization/RolePermission.cs (join + audit)
public sealed class RolePermission : Entity<Guid>
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }   // permisos temporales
}

// Domain/Authorization/PermissionAuditLog.cs (append-only)
public sealed class PermissionAuditLog : Entity<Guid>
{
    public Guid? UserId { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? PermissionId { get; private set; }
    public PermissionAuditAction Action { get; private set; } // Granted, Revoked, RoleAssigned, RoleUnassigned
    public Guid PerformedBy { get; private set; }
    public DateTime PerformedAt { get; private set; }
    public string? Reason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
}
```

## 4. JWT y `PermissionsSnapshot` — patrón heredado de NHCS

> **Contexto histórico**: en el proyecto NHCS, el JWT con permisos del rol se almacenaba en
> `IdentityRole.ConcurrencyStamp` y `CustomAuthorizeAttribute` lo leía para autorizar.
> Esa idea es buena (snapshot versionado, sin recalcular en cada request) pero el campo
> `ConcurrencyStamp` tiene otro propósito (concurrency control de Identity).

**La plantilla mantiene el patrón pero corrige la semántica**:

```csharp
// ❌ Legacy NHCS — abuso de ConcurrencyStamp
role.ConcurrencyStamp = JwtHelper.GenerateRoleToken(rol, _configuration);

// ✅ Plantilla — campo dedicado
role.RegeneratePermissionsSnapshot(jwtSigner.GenerateRoleSnapshot(role));
```

### Contenido del JWT de access token (usuario)

```json
{
  "sub": "user-id",
  "tid": "tenant-id",
  "roles": ["nurse", "billing-clerk"],
  "perms": ["patient.view.own", "prescription.sign", "invoice.export.pdf"],
  "psv": "abc123...",                  // permissions snapshot version (hash)
  "amr": ["pwd", "mfa"],               // métodos de autenticación
  "iat": 1714521600,
  "exp": 1714522500,                    // 15 min
  "jti": "..."
}
```

**Rules**:
- El access token incluye **`perms`** (permisos resueltos) y **`psv`** (versión de snapshot).
- Si `psv` no coincide con el hash actual de los permisos del usuario → **401 + force refresh**.
- TTL del access token: **≤ 15 min**. TTL del refresh token: **7 días**, con rotation y reuse detection.

## 5. Autorización en endpoints

### 5.1 Atributo declarativo `RequirePermission`

```csharp
// Application/Common/Authorization/RequirePermissionAttribute.cs
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequirePermissionAttribute(params string[] permissions) : AuthorizeAttribute
{
    public string[] Permissions { get; } = permissions;
}

// Uso en Minimal API
group.MapPost("/", CreatePatientAsync)
    .WithName("CreatePatient")
    .RequirePermission("patient.create");

group.MapPost("/{id:guid}/sign", SignPrescriptionAsync)
    .RequirePermission("prescription.sign");      // marca el endpoint como sensible
```

### 5.2 Resource-based authorization (ABAC)

Para reglas como "solo puede ver los pacientes asignados a su clínica":

```csharp
public sealed class PatientAuthorizationHandler(ICurrentUser currentUser)
    : AuthorizationHandler<PermissionRequirement, Patient>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement,
        Patient resource)
    {
        // Si tiene permiso global → OK
        if (currentUser.HasPermission("patient.view"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Si tiene permiso .own y es la clínica del usuario → OK
        if (currentUser.HasPermission("patient.view.own") &&
            resource.ClinicId == currentUser.ClinicId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
```

### 5.3 Permisos sensibles — step-up auth

Endpoints con `permission.IsSensitive = true` requieren:
- Claim `amr` con `"mfa"` reciente (≤ 5 min) → si no, devolver `403` con `WWW-Authenticate: Step-up`.
- Auditoría obligatoria del intento (éxito o fallo).

## 6. Cache de permisos — Redis

### Estrategia
- **Key**: `perms:user:{userId}:tenant:{tenantId}` → JSON `{ "psv": "...", "perms": [...] }`
- **TTL**: 15 min (igual al access token)
- **Invalidación**: `UserPermissionsChanged` domain event → `IDistributedCache.RemoveAsync(key)`

```csharp
public sealed class UserPermissionsChangedHandler(IDistributedCache cache)
    : INotificationHandler<UserPermissionsChanged>
{
    public Task Handle(UserPermissionsChanged @event, CancellationToken ct) =>
        cache.RemoveAsync(CacheKeys.UserPermissions(@event.UserId, @event.TenantId), ct);
}
```

### Cuándo se dispara `UserPermissionsChanged`
- Asignación o revocación de rol al usuario
- Modificación de permisos del rol que tiene asignado
- Desactivación del usuario (cache = lista vacía)
- Cambio de `TenantId` del usuario

## 7. Roles del sistema (seed inicial)

| Rol | Descripción | Permisos |
|---|---|---|
| `super-admin` | Acceso total cross-tenant | `*` |
| `tenant-admin` | Admin del tenant | Todos los del tenant excepto `tenant.*` |
| `auditor` | Solo lectura + audit logs | `*.view`, `audit.read` |
| `user` | Usuario base | (ninguno por defecto) |

Los roles `IsSystem = true` no se pueden borrar ni renombrar.

## 8. OWASP Top 10 — A01 Broken Access Control checklist

- ✅ **Deny by default**: `FallbackPolicy = RequireAuthenticatedUser()`
- ✅ **No permisos en cliente**: el frontend lee `perms` del JWT solo para UI; el backend valida siempre
- ✅ **Resource ownership**: handlers ABAC para `*.own`
- ✅ **No IDOR**: todo `id` se valida contra `TenantId` del usuario
- ✅ **Audit log inmutable**: append-only, sin DELETE/UPDATE permitidos
- ✅ **Rate limit en endpoints de cambio de permiso**: 10/min por IP
- ✅ **Step-up MFA en permisos sensibles**: `prescription.sign`, `role.assign`, etc.
- ✅ **Sesiones invalidables**: `UserPermissionsChanged` → cache evict + token version bump

## 9. Anti-patterns prohibidos

```csharp
// ❌ Hardcoded enum (legacy NHCS)
if (user.Module == ModuleEnum.Patient && user.Permission == PermissionEnum.Ver) { ... }

// ❌ String comparison case-insensitive en autorización
if (permission.ToLower() == "patient.view") { ... }   // usa OrdinalIgnoreCase explícito o == directo

// ❌ Permisos en claims sin firmar
context.User.Claims.Where(c => c.Type == "permissions")   // sin validar firma del JWT

// ❌ Bypass de autorización en handler
if (currentUser.IsAdmin) skipPermissionCheck();          // siempre evaluar permisos resueltos

// ❌ Mezclar idiomas en códigos de permiso
"paciente.ver", "Patient.View", "patient_create"
```
