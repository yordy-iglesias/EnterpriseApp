# modulos (dónde vive cada cosa y su responsabilidad)

> Mapa detallado por proyecto/carpeta. Complementa la estructura resumida en `CLAUDE.md`.
> Pares interface↔implementación y en qué capa/ensamblado viven.

## EnterpriseApp.Domain  (sin dependencias NuGet)

### `Common/`
- `BaseEntity<TId>` — Id fuertemente tipado + lista de domain events (`_domainEvents`).
  `RaiseDomainEvent(IDomainEvent)` / `ClearDomainEvents()`.
- `AuditableEntity<TId>` : `BaseEntity<TId>` — añade `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted/DeletedAt/DeletedBy`.
  `SoftDelete(userId)` marca `IsDeleted=true`.
- `ValueObject` — base para value objects con equality estructural.
- `Result<T>` / `Result` — railway pattern. `Result.Success(value)` / `Result.Failure<T>(error)`.
- `Error` — tipo de error con `Code` y `Message`.
- `PagedList<T>` — paginación.
- `IDomainEvent` — marcador para domain events.
- `IAuditInfo` — interfaz de auditoría.

### `Entities/`
- `TodoItem` : `AuditableEntity<TodoId>` — aggregate de ejemplo.
  Factory `Create(...)`, métodos `Update/Complete/Cancel/StartProgress`.
  Fuertemente tipado con `TodoId(Guid)`.
- **Authorization/**:
  - `Permission` : `AuditableEntity<PermissionId>` — código `{module}.{action}[.qualifier]`,
    `Module`, `Action`, `DisplayName`, `IsSensitive`, `IsSystem`, `TenantId?`.
  - `Role` : `AuditableEntity<RoleId>` — `Name`, `NormalizedName`, `TenantId?`, `ParentRoleId?`,
    `IsSystem`, `PermissionsSnapshot` (JWT firmado), `PermissionsSnapshotUpdatedAt`.
    Métodos: `Rename`, `GrantPermission` (emite `PermissionGranted` + `RolePermissionsChanged`),
    `RevokePermission` (emite `PermissionRevoked` + `RolePermissionsChanged`), `Delete` (emite `RoleDeleted`).
  - `RolePermission` : `BaseEntity<RolePermissionId>` — join table `RoleId + PermissionId + GrantedBy + ExpiresAt?`.
  - `UserRole` : `BaseEntity<UserRoleId>` — `UserId (string) + RoleId + AssignedBy + TenantId?`.
  - `PermissionAuditLog` : `BaseEntity<PermissionAuditLogId>` — append-only, campos de auditoría
    (acción, actores, IP, userAgent, correlationId, metadataJson).

### `ValueObjects/`
- `PermissionCode` — wraps string; valida formato `{module}.{action}[.qualifier]`.
- `TodoPriority` — enum-like value object (Low/Medium/High/Critical).

### `DomainEvents/`
- `TodoItemEvents`: `TodoItemCreatedEvent`, `TodoItemCompletedEvent`.
- `Authorization/AuthorizationEvents`: `RoleCreated`, `RoleDeleted`, `PermissionGranted`,
  `PermissionRevoked`, `RolePermissionsChanged`, `UserRoleAssigned`, `UserRoleUnassigned`.

### `Interfaces/Repositories/`
- `IUnitOfWork` — `SaveChangesAsync`.
- `ITodoRepository` — `GetByIdAsync`, `GetPagedAsync`, `Add`, `Update`, `Remove`.
- **Authorization/**: `IRoleRepository`, `IPermissionRepository`, `IUserRoleRepository`,
  `IPermissionAuditLogRepository`.

### `Exceptions/`
- `DomainException` — invariant violations que no deberían ocurrir.

### `Authorization/`
- `PermissionCodes` — constantes de códigos de permiso del sistema (string).

---

## EnterpriseApp.Application  (depende solo de Domain)

### `Common/Behaviors/`
- `LoggingBehavior<TReq, TRes>` — loguea request y duración.
- `ValidationBehavior<TReq, TRes>` — ejecuta FluentValidation; lanza `ValidationException`.
- `CachingBehavior<TReq, TRes>` — activo si `TReq : ICachedQuery<TRes>`.
- `PerformanceBehavior<TReq, TRes>` — warning si >500 ms.

### `Common/Interfaces/`
- `IApplicationDbContext` — contrato del DbContext expuesto a Application.
- `ICurrentUserService` — `UserId`, `IsAuthenticated`.
- `ICacheService` — `GetAsync<T>`, `SetAsync<T>`, `RemoveAsync`, `RemoveByPrefixAsync`.
- `ICachedQuery<TResponse>` — marcador para queries cacheables; implementar con `CacheKey` y `Expiration`.

### `Common/Authorization/`
- `ICurrentUser` — acceso rico al principal: `UserId`, `Permissions`, `TenantId`, `HasPermission(code)`.
- `IPermissionService` — `GetUserPermissionsAsync`, `HasPermissionAsync`.
- `AuthorizationPolicyConstants` — constantes de políticas.
- `CacheKeys.Authorization` — factory de cache keys para permisos.

### `Common/Errors/`
- `AuthorizationErrors` — errores de autorización (NotFound, Forbidden, etc.).

### `Common/Exceptions/`
- `ValidationException` — envuelve errores de FluentValidation.

### `Features/TodoItems/`
Comandos: `CreateTodo`, `UpdateTodo`, `DeleteTodo`, `CompleteTodo` (cada uno con handler + validator).
Queries: `GetTodoById`, `GetPagedTodos` (con validator de paginación).
DTOs: `TodoItemDto`, `PagedTodosDto`.
Mappings: `TodoItemMappingProfile`.

### `Features/Authorization/`
Roles commands: `CreateRole`, `DeleteRole`, `GrantPermission`, `RevokePermission`,
               `AssignRoleToUser`, `UnassignRoleFromUser`.
Roles queries: `GetRoles`, `GetRoleById`.
Permissions queries: `GetPermissions`.
Users queries: `GetUserPermissions`.
EventHandlers: `AuditLogHandlers` (escribe `PermissionAuditLog`), `RolePermissionsChangedHandler`
               (regenera snapshot), `UserPermissionsChangedHandler` (evicta cache Redis).
DTOs: `AuthorizationDtos`.

### `DependencyInjection/`
- `ApplicationServiceExtensions.AddApplicationServices()` — AutoMapper + FluentValidation + MediatR + behaviors.

---

## EnterpriseApp.Infrastructure  (depende de Application + Domain)

### `Persistence/`
- `AppDbContext` — EF Core 9, implementa `IApplicationDbContext`. DbSets: `TodoItems`, `Permissions`,
  `Roles`, `RolePermissions`, `UserRoles`, `PermissionAuditLogs`.
  `OnModelCreating`: `ApplyConfigurationsFromAssembly` + `HasQueryFilter(!IsDeleted)` por agregado.
- `AppDbContextFactory` : `IDesignTimeDbContextFactory<AppDbContext>` — para `dotnet ef migrations`.
- `DatabaseOptions` — `Provider`, `ConnectionStringName`, `MaxRetryCount`, `CommandTimeoutSeconds`,
  `EnableSensitiveDataLogging`, `EnableDetailedErrors`.
- `DatabaseProvider` enum — `PostgreSQL | SqlServer | SQLite`.
- `Configurations/` — `IEntityTypeConfiguration<T>` por entidad:
  - `TodoItemConfiguration`, `RoleConfiguration`, `PermissionConfiguration`,
    `RolePermissionConfiguration`, `UserRoleConfiguration`, `PermissionAuditLogConfiguration`.
- `Interceptors/` — `AuditableEntitySaveChangesInterceptor`, `DomainEventDispatcherInterceptor`.
- `Seeds/DatabaseSeeder` — siembra roles/permisos del sistema (idempotente).
  `Seeds/PermissionsSeed` — lista de permisos semilla.

### `Repositories/`
- `TodoRepository` : `ITodoRepository`.
- `UnitOfWork` : `IUnitOfWork` — wraps `AppDbContext.SaveChangesAsync`.
- **Authorization/**: `RoleRepository`, `PermissionRepository`, `UserRoleRepository`,
  `PermissionAuditLogRepository`.

### `Authorization/`
- `PermissionPolicyProvider` : `IAuthorizationPolicyProvider` (singleton) — crea políticas `perm:{code}` on-demand.
- `PermissionRequirement` : `IAuthorizationRequirement`.
- `PermissionAuthorizationHandler` : `AuthorizationHandler<PermissionRequirement>` — delega en `IPermissionService`.
- `StepUpMfaAuthorizationHandler` — verifica claim `amr` para permisos sensibles.
- `PermissionService` : `IPermissionService` — resuelve y cachea permisos del usuario.

### `Caching/`
- `RedisCacheService` : `ICacheService` — wraps `IDistributedCache` + `HybridCache`.
  Deserializa con `System.Text.Json`.

### `DependencyInjection/`
- `InfrastructureServiceExtensions.AddInfrastructureServices(config)` — registra todo lo anterior.

---

## EnterpriseApp.API  (depende de Application + Infrastructure)

### `Controllers/`
- `TodosController` — CRUD TodoItems via MediatR.
- `RolesController` — gestión de roles y permisos.
- `PermissionsController` — lista de permisos disponibles.

### `Filters/`
- `RequirePermissionAttribute` — atributo declarativo que envuelve `AuthorizeAttribute` con `perm:{code}`.

### `Middleware/`
- `GlobalExceptionMiddleware` — convierte excepciones a `ProblemDetails` RFC 7807:
  400 = `ValidationException`, 404 = `NotFoundException`, 422 = `DomainException`, 500 = inesperado.

### `Services/`
- `CurrentUserService` : `ICurrentUserService` — lee `ClaimsPrincipal` del `HttpContext`.
- `CurrentUser` : `ICurrentUser` — principal rico con permisos, tenant, AMR.

### `Program.cs` — entry point / composition root. Ver [[arquitectura]].

---

## EnterpriseApp.AppHost  (solo desarrollo/staging)
- `Program.cs` — Aspire AppHost: referencia la API, PostgreSQL, Redis, etc.

## EnterpriseApp.ServiceDefaults
- `Extensions.cs` — `AddServiceDefaults()`: OTel, health checks, service discovery, HttpClient resilience.

## Tests
- `EnterpriseApp.Domain.Tests` — unit tests de entidades/value objects (xUnit + FluentAssertions).
- `EnterpriseApp.Application.Tests` — unit tests de handlers (Moq).
- `EnterpriseApp.ArchitectureTests` — reglas de arquitectura (NetArchTest.Rules).
- `EnterpriseApp.Architecture.Tests` — tests de autorización arquitectural.

Ver [[arquitectura]], [[esquema-db]], [[convenciones]].
