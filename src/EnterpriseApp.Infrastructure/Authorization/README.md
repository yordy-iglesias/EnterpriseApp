# Authorization module — reference implementation

Implementation companion to `.claude/rules/authorization.md` and `.claude/rules/audit-logging.md`.

## File map

| Layer | File | Responsibility |
|---|---|---|
| Domain | `Entities/Authorization/Permission.cs` | Atomic capability with `PermissionCode` VO |
| Domain | `Entities/Authorization/Role.cs` | Aggregate root — owns `RolePermissions` and `PermissionsSnapshot` |
| Domain | `Entities/Authorization/RolePermission.cs` | Join + grant audit |
| Domain | `Entities/Authorization/UserRole.cs` | User ↔ Role assignment (string `UserId` for Identity-agnosticism) |
| Domain | `Entities/Authorization/PermissionAuditLog.cs` | Append-only audit row |
| Domain | `ValueObjects/PermissionCode.cs` | `{module}.{action}[.{qualifier}]` validator |
| Domain | `Authorization/PermissionCodes.cs` | Canonical permission constants |
| Domain | `DomainEvents/Authorization/AuthorizationEvents.cs` | Events emitted by aggregates |
| Application | `Common/Authorization/ICurrentUser.cs` | Rich principal abstraction (perms, tenant, AMR) |
| Application | `Common/Authorization/IPermissionService.cs` | Resolves user → permission set, signs role snapshots |
| Application | `Common/Authorization/AuthorizationPolicyConstants.cs` | Shared policy-name strings |
| Application | `Features/Authorization/...` | MediatR commands/queries + audit handlers |
| Infrastructure | `Authorization/PermissionService.cs` | Resolves perms via DB + Redis cache, signs snapshots |
| Infrastructure | `Authorization/PermissionAuthorizationHandler.cs` | `IAuthorizationHandler` for `PermissionRequirement` |
| Infrastructure | `Authorization/StepUpMfaAuthorizationHandler.cs` | Enforces recent MFA |
| Infrastructure | `Authorization/PermissionPolicyProvider.cs` | Materialises `perm:{code}` policies on demand |
| Infrastructure | `Persistence/Configurations/Authorization/*.cs` | EF Core fluent config |
| Infrastructure | `Persistence/Seeds/PermissionsSeed.cs` | Static seed manifest |
| Infrastructure | `Persistence/Seeds/DatabaseSeeder.cs` | Idempotent runtime seeder |
| API | `Filters/RequirePermissionAttribute.cs` | `[RequirePermission(...)]` + `[RequireStepUpMfa]` |
| API | `Services/CurrentUser.cs` | `ICurrentUser` impl over `HttpContext` |
| API | `Controllers/RolesController.cs` | CRUD + grant/revoke + assign/unassign |
| API | `Controllers/PermissionsController.cs` | Read-only catalog + per-user resolved perms |

## First-time setup

1. **Add the EF migration:**
   ```bash
   dotnet ef migrations add InitAuthorization \
       -p src/EnterpriseApp.Infrastructure \
       -s src/EnterpriseApp.API \
       -o Persistence/Migrations
   ```
2. **Add the anti-update/delete trigger** to the generated migration's `Up()` (after the
   `CreateTable("PermissionAuditLog")` call):
   ```csharp
   migrationBuilder.Sql(@"
       CREATE TRIGGER tr_PermissionAuditLog_NoUpdateDelete
       ON auth.PermissionAuditLog
       INSTEAD OF UPDATE, DELETE
       AS BEGIN
           THROW 50001, 'PermissionAuditLog is append-only', 1;
       END");
   ```
   Add a matching `DROP TRIGGER` to `Down()`.
3. **Apply migration and seed:** `dotnet ef database update`. Seed runs on startup
   via `DatabaseSeeder` registered in `Program.cs`.
4. **JWT issuance:** when issuing access tokens, embed the user's resolved permissions:
   - `perms`: JSON-encoded array of permission codes
   - `psv`: snapshot version hash from `IPermissionService.ComputeSnapshotVersion`
   - `tid`: tenant id (or empty for global users)
   - `auth_time` + `amr`: required for step-up MFA enforcement.

## Migration from legacy NHCS

For existing NHCS databases moving to this template, see
`.claude/agents/access-control-auditor.md` for the audit playbook. Key migration steps:
1. Map every `ModuleEnum` × `PermissionEnum` combination to a permission code.
2. Bulk-insert into `auth.Permissions` with `IsSystem = false`.
3. Translate each `IdentityRole.ConcurrencyStamp` JWT into rows in `auth.RolePermissions`,
   then call `IPermissionService.SignRoleSnapshot` to populate `auth.Roles.PermissionsSnapshot`.
4. Migrate `IdentityUserRole<string>` rows into `auth.UserRoles` with synthesized
   `AssignedBy = "legacy-migration"`.
5. Restore `IdentityRole.ConcurrencyStamp` to ASP.NET Core Identity's normal usage
   (concurrency check, not permission storage).
