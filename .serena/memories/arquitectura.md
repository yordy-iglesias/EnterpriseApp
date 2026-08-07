# arquitectura (detalle profundo)

> Complementa `CLAUDE.md` (visión general). Aquí va el flujo exacto clase→método, el modelo de
> concurrencia y los puntos frágiles. Verifica contra el código antes de tocar zonas sensibles.

## Secuencia de arranque — `Program.cs`

1. Bootstrap logger (Serilog) antes de que DI esté listo.
2. `builder.AddServiceDefaults()` — Aspire: OTel (AspNetCore + Http + Runtime + OTLP), service
   discovery, `HttpClient` con resiliencia estándar. Agrega health check `self` (liveness).
3. `builder.Host.UseSerilog(...)` — Serilog completo leyendo `appsettings.json`.
4. `builder.Services.AddApplicationServices()` — AutoMapper, FluentValidation, MediatR + behaviors.
5. `builder.Services.AddInfrastructureServices(config)` — DbContext (proveedor dinámico),
   interceptores EF, repositorios, autorización RBAC, seeder, cache.
6. JWT Bearer + `FallbackPolicy = RequireAuthenticatedUser` — todo requiere auth por defecto.
7. `app.UseMiddleware<GlobalExceptionMiddleware>()` — primer middleware; convierte excepciones a ProblemDetails RFC 7807.
8. Scalar/OpenAPI solo en Development.
9. `app.MapControllers()` + `app.MapDefaultEndpoints()` (health/alive).
10. `db.Database.MigrateAsync()` + `seeder.SeedAsync()` al arrancar (idempotente).

## Pipeline MediatR — `ApplicationServiceExtensions`

```
Request → LoggingBehavior → ValidationBehavior → CachingBehavior → PerformanceBehavior → Handler
```

- **LoggingBehavior**: loguea nombre de request y duración.
- **ValidationBehavior**: ejecuta todos los `IValidator<TRequest>` registrados; lanza `ValidationException` si falla.
- **CachingBehavior**: activo solo si el request implementa `ICachedQuery<TResponse>`; salta en Commands.
- **PerformanceBehavior**: advierte si el handler tarda >500 ms.

## Interceptores EF Core — `AppDbContext`

`AppDbContext(options, AuditableEntitySaveChangesInterceptor?, DomainEventDispatcherInterceptor?)`

- **`AuditableEntitySaveChangesInterceptor`**: antes de `SaveChanges`, estampa
  `CreatedAt/CreatedBy` (entidades nuevas) y `UpdatedAt/UpdatedBy` (modificadas).
  Lee `ICurrentUserService` para el `userId`.
- **`DomainEventDispatcherInterceptor`**: *después* de `SaveChanges`, publica los domain events
  acumulados (`IDomainEvent`) via `IPublisher` (MediatR). Garantiza consistencia: eventos solo se
  dispatcan si la transacción se confirmó. El interceptor llama `ClearDomainEvents()` en cada agregado.

## Flujo de domain events

1. Método de dominio llama `RaiseDomainEvent(new SomeEvent(...))` → se acumula en `_domainEvents`.
2. `SaveChangesAsync` completa → `DomainEventDispatcherInterceptor` itera todos los agregados raíz.
3. Cada evento se publica con `IPublisher.Publish(domainEventNotification, ct)`.
4. Handlers suscritos (`INotificationHandler<DomainEventNotification<T>>`) se ejecutan en el mismo
   scope/transacción de la request HTTP. Si necesitan transacción separada, deben crear un nuevo scope.

## Flujo de autorización RBAC+ABAC

1. `PermissionPolicyProvider` (singleton) materializa políticas `perm:{code}` bajo demanda.
2. `PermissionAuthorizationHandler` resuelve el requirement: llama `IPermissionService.HasPermissionAsync`.
3. `PermissionService` busca permisos del usuario en Redis (`perms:user:{id}:tenant:{tid}`); si falta,
   resuelve contra BD (`IUserRoleRepository` + `IRoleRepository`) y guarda en cache (TTL 15 min).
4. `UserPermissionsChangedHandler` escucha eventos `UserPermissionsChanged` / `RolePermissionsChanged`
   y evicta la entrada Redis correspondiente.
5. Permisos sensibles pasan además por `StepUpMfaAuthorizationHandler` (claim `amr` con `mfa` reciente).

## Flujo de selección de proveedor BD — `AppDbContextFactory.ApplyProvider`

- Lee `Database:Provider` de configuración (`PostgreSQL` | `SqlServer` | `SQLite`).
- Configura EF Core con Npgsql / SQL Server / SQLite según el valor.
- Cuando no hay Redis (`ConnectionStrings:Redis` vacío) o el proveedor es `SQLite`, la cache es
  solo en-memoria (`IMemoryCache` + `HybridCache` sin backend distribuido).

## Invariantes / puntos frágiles

- **DI**: todo nuevo repositorio/servicio debe registrarse en `InfrastructureServiceExtensions.cs`.
- **Interceptores opcionales**: `AppDbContext` acepta `null` en interceptores para soportar el factory
  design-time (`AppDbContextFactory`); sin embargo en runtime siempre están presentes.
- **Soft-delete**: `HasQueryFilter(e => !e.IsDeleted)` solo está en `TodoItem`. Al agregar un nuevo
  aggregate, añadir el filtro en `OnModelCreating` o las queries devolverán registros eliminados.
- **Migrations por proveedor**: las migraciones actuales son para el proveedor activo al crearlas.
  Cambiar de proveedor puede requerir regenerar migraciones.

Ver también memorias [[modulos]], [[esquema-db]], [[convenciones]].
