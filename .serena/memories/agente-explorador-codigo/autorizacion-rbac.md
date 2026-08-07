# Autorización RBAC — flujo detallado y puntos de extensión

> Complementa [[arquitectura]] §Flujo de autorización. Aquí el flujo completo para
> proteger un endpoint y los puntos donde extender el modelo.

## Cómo proteger un endpoint con permiso

```csharp
// En el controller:
[HttpPost]
[RequirePermission("patient.create")]
public async Task<IActionResult> CreateAsync(...) { ... }
```

`RequirePermissionAttribute` es un `AuthorizeAttribute` con `Policy = "perm:patient.create"`.

## Flujo de evaluación

```
Request llega al controller decorado con [RequirePermission("patient.create")]
  → ASP.NET Authorization middleware
  → PermissionPolicyProvider.GetPolicyAsync("perm:patient.create")
      → Si la política no existe aún → CreatePolicy con PermissionRequirement("patient.create")
  → PermissionAuthorizationHandler.HandleRequirementAsync
      → IPermissionService.HasPermissionAsync(userId, tenantId, "patient.create")
          → ICacheService.GetAsync<string[]>($"perms:user:{userId}:tenant:{tid}")
            → HIT: comparar; MISS → resolver de BD
          → IUserRoleRepository.GetUserRolesAsync(userId, tenantId)
          → IRoleRepository.GetRoleWithPermissionsAsync(roleId) por cada rol
          → flatten permisos resueltos + guardar en cache (TTL 15 min)
      → Si HasPermission → context.Succeed(requirement)
      → Si no → context.Fail (→ 403 Forbidden)
```

## Invalidación de cache

Cuando se modifica la asignación de roles/permisos:
- `UserPermissionsChangedHandler` recibe `UserRoleAssigned` / `UserRoleUnassigned`
  → `ICacheService.RemoveAsync($"perms:user:{userId}:tenant:{tid}")`
- `RolePermissionsChangedHandler` recibe `RolePermissionsChanged`
  → invalida cache de todos los usuarios con ese rol (actualmente evicta por prefijo).

## Step-up MFA (permisos sensibles)

`StepUpMfaAuthorizationHandler` se ejecuta para permisos con `IsSensitive=true`:
- Verifica que el JWT tenga claim `amr` con valor `mfa` en los últimos 5 min.
- Si no → responde 403 con header `WWW-Authenticate: Step-up` para que el cliente
  solicite re-autenticación con MFA.

## Extensión: autorización por recurso (ABAC)

Para "solo ver recursos propios":
```csharp
// En el handler, después de recuperar el recurso:
if (!currentUser.HasPermission("patient.view") &&
    !(currentUser.HasPermission("patient.view.own") && resource.OwnerId == currentUser.UserId))
    throw new ForbiddenException();
```

O via `IAuthorizationService.AuthorizeAsync(user, resource, requirement)` con un
`AuthorizationHandler<PermissionRequirement, Patient>` dedicado.

## Roles del sistema (seed)

`DatabaseSeeder.SeedAsync()` crea roles iniciales con `IsSystem=true`:
- `super-admin` — acceso total.
- `tenant-admin` — admin del tenant.
- `auditor` — solo lectura + audit logs.
- `user` — sin permisos por defecto.

Los permisos semilla están en `PermissionsSeed.cs`.
Roles con `IsSystem=true` lanzan `DomainException` si se intenta borrarlos o renombrarlos.
