# esquema-db (entidades EF Core y schema)

> Detalle tabla-por-tabla vía EF Core 9. Configuración con `IEntityTypeConfiguration<T>` en
> `Infrastructure/Persistence/Configurations/`. Una sola BD, un solo `AppDbContext`.
> Verifica contra el código antes de cambios de esquema.

## Contexto y migraciones

- **Un único `AppDbContext`** (`EnterpriseApp.Infrastructure.Persistence`).
- **Migraciones por proveedor** (las actuales son para el proveedor activo cuando se generaron):
  ```bash
  dotnet ef migrations add <Name> \
    --project src/EnterpriseApp.Infrastructure \
    --startup-project src/EnterpriseApp.API
  ```
- `HasQueryFilter(e => !e.IsDeleted)` se aplica en `OnModelCreating` por cada aggregate con soft-delete.
  Si se agrega un nuevo aggregate, añadir el filtro ahí o las queries devolverán registros eliminados.
- Migraciones se aplican automáticamente al arrancar (`db.Database.MigrateAsync()` en Program.cs).

## DbSets de `AppDbContext`

| DbSet | Entidad | Tabla (convención) |
|---|---|---|
| `TodoItems` | `TodoItem` | `TodoItems` |
| `Permissions` | `Permission` | `Permissions` |
| `Roles` | `Role` | `Roles` |
| `RolePermissions` | `RolePermission` | `RolePermissions` |
| `UserRoles` | `UserRole` | `UserRoles` |
| `PermissionAuditLogs` | `PermissionAuditLog` | `PermissionAuditLogs` |

## Entidades y columnas clave

### TodoItem — aggregate de ejemplo
PK: `TodoId` (Guid, `HasConversion` en configuración)
Columnas: `Title (nvarchar 200)`, `Description (nvarchar 1000)?`, `Priority (int)`, `Status (int)`,
`DueDate (datetimeoffset)?`
Audit: `CreatedAt`, `CreatedBy`, `UpdatedAt?`, `UpdatedBy?`
Soft-delete: `IsDeleted`, `DeletedAt?`, `DeletedBy?`
QueryFilter: `!IsDeleted`

### Permission
PK: `PermissionId` (Guid)
Columnas: `Code (nvarchar 100, unique)`, `Module (nvarchar 50)`, `Action (nvarchar 50)`,
`DisplayName (nvarchar 200)`, `Description (nvarchar 500)?`,
`IsSensitive (bit)`, `IsSystem (bit)`, `TenantId (uniqueidentifier)?`
Audit: `CreatedAt`, `CreatedBy`, `UpdatedAt?`, `UpdatedBy?`
FK: `←` N `RolePermission.PermissionId`

### Role
PK: `RoleId` (Guid)
Columnas: `Name (nvarchar 100)`, `NormalizedName (nvarchar 100, unique)`,
`Description (nvarchar 500)?`, `TenantId (uniqueidentifier)?`,
`ParentRoleId (uniqueidentifier)?` → self-referential FK,
`IsSystem (bit)`,
`PermissionsSnapshot (nvarchar(max))?`, `PermissionsSnapshotUpdatedAt (datetimeoffset)?`
Audit + soft-delete
FK: `←` N `RolePermission.RoleId`, `←` N `UserRole.RoleId`

### RolePermission  (join table con auditoría)
PK: `RolePermissionId` (Guid)
Columnas: `RoleId`, `PermissionId`, `GrantedBy (nvarchar 450)`, `GrantedAt (datetimeoffset)`,
`ExpiresAt (datetimeoffset)?`
Índice único: `(RoleId, PermissionId)`

### UserRole  (join table con auditoría)
PK: `UserRoleId` (Guid)
Columnas: `UserId (nvarchar 450)`, `RoleId`, `AssignedBy (nvarchar 450)`,
`AssignedAt (datetimeoffset)`, `TenantId (uniqueidentifier)?`
Índice único: `(UserId, RoleId, TenantId)`

### PermissionAuditLog  (append-only)
PK: `PermissionAuditLogId` (Guid)
Columnas: `Action (int)` (PermissionAuditAction enum),
`PerformedBy (nvarchar 450)`, `PerformedByEmail (nvarchar 254)?`, `PerformedAt (datetimeoffset)`,
`TargetUserId (nvarchar 450)?`, `TargetRoleId?`, `TargetPermissionId?`,
`Reason (nvarchar 500)?`, `IpAddress (nvarchar 45)?`, `UserAgent (nvarchar 512)?`,
`CorrelationId (nvarchar 32)?`, `TenantId?`, `MetadataJson (nvarchar(max))?`
**Solo INSERT**: el repositorio no expone Update ni Delete. No hay trigger DB todavía (ver checklist de audit-logging).

## Configuraciones EF destacadas

- **IDs fuertemente tipados**: cada configuración declara
  `HasConversion(id => id.Value, v => XxxId.From(v))` para el PK y FKs.
- **`ApplyConfigurationsFromAssembly`** en `OnModelCreating` — todas las `IEntityTypeConfiguration<T>`
  del ensamblado Infrastructure se aplican automáticamente.
- **`PermissionAuditLogConfiguration`** — sin índice de modificación; considera agregar
  trigger DB append-only como describe `.claude/rules/audit-logging.md §3`.

## Notas

- EF genera migraciones por proveedor activo; cambiar de PostgreSQL a SQL Server puede requerir
  regenerar migraciones o usar migraciones multi-provider.
- No hay stored procedures ni raw SQL — solo LINQ + EF Core.
- `AppDbContextFactory` (`IDesignTimeDbContextFactory`) lee la configuración para `dotnet ef` sin
  necesitar el host completo; interceptores son opcionales (null-safe) en ese path.

Ver [[arquitectura]], [[modulos]], [[convenciones]].
