---
name: permission-review
description: |
  Auto-invoked review of authorization in the codebase. Triggers when the diff
  touches endpoints, MediatR handlers, `[RequirePermission]` attributes, role/permission
  seeds, EF migrations affecting `Roles`, `Permissions`, `RolePermission`, `UserRole`,
  or `PermissionAuditLog`. Use when the user asks to "review permissions",
  "check authorization", "audit access control", "validate the auth model",
  or after `/generate-permission` and `/generate-endpoint` commands.
---

# Permission Review — Auto-invoked authorization audit

You are reviewing changes that affect authorization, roles, or permissions in a
.NET 9 Clean Architecture codebase. Your job is to enforce the rules in
`.claude/rules/authorization.md` and `.claude/rules/audit-logging.md`.

Run the checks below in order. Stop and report immediately on any **CRITICAL** finding.

## 1. Endpoint authorization coverage (CRITICAL)

For every endpoint added or modified in the diff:

- [ ] Has `[RequirePermission(...)]` or `.RequirePermission(...)` (Minimal API)
      OR is explicitly marked `[AllowAnonymous]` with a justification comment.
- [ ] If the endpoint is `POST/PUT/PATCH/DELETE` on a resource with `TenantId`,
      verify the handler validates `tenant ownership` (resource.TenantId == currentUser.TenantId).
- [ ] If the permission code ends in `.own`, the handler enforces ABAC ownership check
      (e.g. `resource.OwnerId == currentUser.Id` or `resource.ClinicId == currentUser.ClinicId`).
- [ ] If the permission is `IsSensitive = true`, the endpoint also has step-up MFA
      requirement (`.RequireStepUpMfa()` or equivalent).

**Anti-patterns to flag**:
```csharp
// ❌ Endpoint sin RequirePermission ni AllowAnonymous
group.MapDelete("/{id:guid}", DeleteAsync);

// ❌ AllowAnonymous sin comentario justificativo
[AllowAnonymous]
public async Task<IActionResult> Delete(...) { }

// ❌ Permission check duplicado en handler (debe ser declarativo en endpoint)
if (!_currentUser.HasPermission("patient.view")) return Forbid();
```

## 2. Permission code conventions (HIGH)

For new permissions in `PermissionCodes.cs` or seed:

- [ ] Code matches regex `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*){1,2}$`.
- [ ] Action is in canonical list: `view, create, update, delete, export, import,
      approve, reject, sign, cancel, assign, revoke, read`.
- [ ] No mixed-language codes (`paciente.ver`, `Patient.View`).
- [ ] No generic `.full-access` or `.admin` qualifiers — break into atomic permissions.
- [ ] No duplicate codes (case-insensitive comparison) in `PermissionsSeed.cs`.

## 3. Seed and migration (HIGH)

For new permissions/roles in seeds:

- [ ] `PermissionsSeed.cs` uses `PermissionSeedItem` records with all required fields.
- [ ] EF migration only contains seed INSERTs (no schema changes mixed in).
- [ ] Migration name follows pattern `Add_Permission_{ModuleAction}` or `Add_Role_{Name}`.
- [ ] If new role is `IsSystem = true`, it cannot be deleted via API.
- [ ] `super-admin` role is not granted explicit permissions (it has `*` wildcard).

## 4. JWT and `PermissionsSnapshot` (HIGH)

If the diff touches `Role.PermissionsSnapshot` or JWT generation:

- [ ] `PermissionsSnapshot` is **never** read from `IdentityRole.ConcurrencyStamp`.
      The legacy NHCS pattern of using `ConcurrencyStamp` for permissions is forbidden
      in this codebase — use the dedicated `PermissionsSnapshot` field.
- [ ] Snapshot regeneration is triggered by `RolePermissionsChanged` domain event.
- [ ] JWT payload includes `psv` (permissions snapshot version) claim.
- [ ] Access token TTL is `≤ 15 minutes`.
- [ ] Refresh token rotation with reuse detection is enabled.

## 5. Cache invalidation (HIGH)

If permissions/roles are modified:

- [ ] A `UserPermissionsChanged` domain event is emitted.
- [ ] A handler subscribes and calls `IDistributedCache.RemoveAsync(CacheKeys.UserPermissions(...))`.
- [ ] The event is dispatched **after** `SaveChangesAsync` (consistency).
- [ ] No code path mutates `RolePermission` without emitting the event.

## 6. Audit logging coverage (CRITICAL)

For every authorization change:

- [ ] A `PermissionAuditLog` row is inserted in the same `UnitOfWork`.
- [ ] `PerformedBy`, `IpAddress`, `UserAgent`, `CorrelationId` are populated.
- [ ] No `UPDATE` or `DELETE` is ever performed on `PermissionAuditLog` (table has trigger).
- [ ] No password, token, or secret is logged anywhere in the diff.

**Grep for sensitive data leaks**:
```
LogInformation.*[Pp]assword
LogInformation.*[Tt]oken
LogInformation.*[Aa]uthorization.*Bearer
```

## 7. Tests required (MEDIUM)

For new permissions or endpoints:

- [ ] Integration test for `403 Forbidden` when missing the permission.
- [ ] Integration test for `200/201` when the permission is present.
- [ ] If `IsSensitive`, test that step-up MFA is enforced (`403` without MFA, `200` with MFA).
- [ ] If `.own` qualifier, test that other-user resource returns `403/404`.
- [ ] Seed test verifies the new permission is present in DB after migration.

## 8. Multi-tenant safety (HIGH)

If the diff touches tenant-scoped resources:

- [ ] EF Core global query filter `HasQueryFilter(e => e.TenantId == _currentUser.TenantId)`
      is configured and not bypassed with `IgnoreQueryFilters()`.
- [ ] Cross-tenant access requires explicit `super-admin` role check + audit log.
- [ ] No endpoint accepts `TenantId` from the request body or query string for filtering
      (it must come from the JWT claim `tid`).

## Reporting format

Output the review as:

```
🔍 Permission Review — {N findings}

CRITICAL:
- [file:line] {description} — see .claude/rules/authorization.md §{section}

HIGH:
- [file:line] {description}

MEDIUM:
- [file:line] {description}

✅ Verified:
- All endpoints have authorization attributes
- Audit log inserts present for all permission changes
- Cache invalidation events emitted

Suggested commands:
- dotnet test --filter Category=Authorization
- /generate-permission to add missing atomic permissions
```

If there are zero findings, end with:
```
✅ No authorization issues detected. Diff complies with .claude/rules/authorization.md.
```

## Escalation

If the diff introduces a **brand-new authorization concept** (new role hierarchy,
new ABAC handler, new tenant boundary), do **not** approve silently. Recommend
that the user invoke the `access-control-auditor` agent for a deeper review against
OWASP ASVS V4.
