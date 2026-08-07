# EnterpriseApp — Clean Architecture Template

> .NET 9 / C# 13 · PostgreSQL | SQL Server | SQLite · Redis · MediatR 12 · EF Core 9

## Quick start
```bash
# SQLite — sin Docker, ideal para desarrollo rápido
bash setup.sh --provider sqlite

# PostgreSQL (default)
docker compose up -d              # arranca postgres + redis
bash setup.sh --provider postgres

# SQL Server 2022
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d
bash setup.sh --provider sqlserver

dotnet run --project src/EnterpriseApp.API
# Scalar UI  → https://localhost:5001/scalar/v1
# Health     → https://localhost:5001/health
```

---

## Architecture overview

```
src/
  EnterpriseApp.Domain          ← Zero NuGet dependencies. Pure C#.
  EnterpriseApp.Application     ← MediatR, FluentValidation, AutoMapper
  EnterpriseApp.Infrastructure  ← EF Core 9, Redis (HybridCache), Serilog
  EnterpriseApp.API             ← ASP.NET Core 9, JWT, Scalar/OpenAPI
tests/
  EnterpriseApp.Domain.Tests        ← xUnit, FluentAssertions
  EnterpriseApp.Application.Tests   ← xUnit, Moq
  EnterpriseApp.ArchitectureTests   ← NetArchTest.Rules
```

### Dependency rule (strictly enforced)
```
API → Application ← Infrastructure
       ↓
     Domain
```
Domain has **zero** dependencies on any other layer.
Application depends only on Domain.
Infrastructure depends on Application + Domain.
API depends on Application + Infrastructure.

---

## Domain layer patterns

### Strongly-typed IDs
All aggregates use a `record` ID wrapper to prevent accidental swaps:
```csharp
public sealed record TodoId(Guid Value)
{
    public static TodoId New()            => new(Guid.NewGuid());
    public static TodoId From(Guid value) => new(value);
}
```
EF Core converts via `HasConversion(id => id.Value, v => TodoId.From(v))`.

### AuditableEntity<TId>
Every aggregate root inherits `AuditableEntity<TId>`:
- `CreatedAt / CreatedBy` — stamped by `AuditableEntitySaveChangesInterceptor`
- `UpdatedAt / UpdatedBy` — stamped on modification
- `IsDeleted / DeletedAt` — soft-delete via `SoftDelete(userId)`

### Domain Events
Raise events via `RaiseDomainEvent(new SomeEvent(...))` in domain methods.
`DomainEventDispatcherInterceptor` publishes them *after* `SaveChangesAsync` via MediatR `IPublisher`.

### Result<T> (railway pattern)
```csharp
Result<TodoItem> result = Result.Success(todo);
Result<TodoItem> failed = Result.Failure<TodoItem>(Error.NotFound);
```
Use `Result<T>` in domain methods when failure is a valid business outcome.
Throw `DomainException` for invariant violations that should never happen.

---

## Application layer patterns

### CQRS with MediatR
- **Commands** return `Guid` (create) or `Unit` (mutate).
- **Queries** return DTOs — never domain entities.
- All commands/queries validated via `ValidationBehavior` before reaching the handler.

### Pipeline order
```
LoggingBehavior → ValidationBehavior → PerformanceBehavior → Handler
```

### FluentValidation conventions
- One validator per command/query, same folder.
- Validator class name: `{CommandName}Validator`.

---

## Infrastructure layer patterns

### Multi-provider database support

| Provider | Config value | Uso recomendado |
|----------|-------------|-----------------|
| `PostgreSQL` | `"PostgreSQL"` | Producción Linux / Docker / cloud |
| `SqlServer`  | `"SqlServer"`  | Producción Windows / Azure SQL |
| `SQLite`     | `"SQLite"`     | Desarrollo local y tests de integración |

**Cambiar proveedor** — editar `appsettings.json` (o `appsettings.Development.json` para local):
```json
"Database": {
  "Provider": "SqlServer",
  "ConnectionStringName": "SqlServerConnection"
}
```

**Variables de entorno** (override en Docker / CI):
```
Database__Provider=SqlServer
Database__ConnectionStringName=SqlServerConnection
```

**Opciones de DatabaseOptions:**
- `Provider` — proveedor activo
- `ConnectionStringName` — nombre de la cadena en `ConnectionStrings`
- `MaxRetryCount` — reintentos en fallas transitorias (default: 3)
- `CommandTimeoutSeconds` — timeout de queries (null = default del proveedor)
- `EnableSensitiveDataLogging` — loguea valores de parámetros (solo desarrollo)
- `EnableDetailedErrors` — errores detallados de EF (solo desarrollo)

### EF Core 9
- `AppDbContext` implements `IApplicationDbContext`.
- Configurations: `IEntityTypeConfiguration<T>` in `Persistence/Configurations/`.
- Global query filter per aggregate: `HasQueryFilter(e => !e.IsDeleted)`.
- `AppDbContextFactory` implementa `IDesignTimeDbContextFactory` — lee la config para `dotnet ef`.
- Migrations por proveedor:
```bash
# PostgreSQL
dotnet ef migrations add <Name> \
  --project src/EnterpriseApp.Infrastructure \
  --startup-project src/EnterpriseApp.API

# SQL Server (override via env var)
Database__Provider=SqlServer dotnet ef migrations add <Name> \
  --project src/EnterpriseApp.Infrastructure \
  --startup-project src/EnterpriseApp.API
```

### Caching (HybridCache)
- L1 = `IMemoryCache` (in-process, 1–2 min TTL)
- L2 = Redis (distributed, 5–10 min TTL)
- Use `ICacheService` in Application — never reference `HybridCache` directly.
- Cache keys convention: `"{entity}:{id}"`, prefix invalidation: `"{entity}:*"`.

---

## API layer conventions

| Method | Route                        | Handler           |
|--------|------------------------------|-------------------|
| GET    | /api/todos?page=&pageSize=   | GetPagedTodos     |
| GET    | /api/todos/{id}              | GetTodoById       |
| POST   | /api/todos                   | CreateTodo        |
| PUT    | /api/todos/{id}              | UpdateTodo        |
| PATCH  | /api/todos/{id}/complete     | CompleteTodo      |
| DELETE | /api/todos/{id}              | DeleteTodo        |

- All errors return RFC 7807 `ProblemDetails` (via `GlobalExceptionMiddleware`).
- 400 = ValidationException, 404 = NotFoundException, 422 = DomainException, 500 = unexpected.

---

## Reglas de trabajo para Claude Code (aplican siempre)

- **Navegación**: si Serena está instalada, usa sus herramientas simbólicas
  (`get_symbols_overview`, `find_symbol`, `find_referencing_symbols`) ANTES de leer archivos
  completos; lee un archivo entero solo cuando lo simbólico no baste, y dilo. Si Serena no está
  disponible, usa Grep/Glob/Read con el mismo criterio.
- **Memorias Serena**: las memorias ORIENTAN, el código MANDA. Cárgalas BAJO DEMANDA según lo que
  necesites: `arquitectura` (flujos clase→método, DI, interceptores), `esquema-db` (EF Core, DbSets,
  configuraciones), `modulos` (mapa de proyectos/carpetas), `convenciones` (recetas, estilo, gotchas),
  `agente-explorador-codigo/pipeline-cqrs` y `agente-explorador-codigo/autorizacion-rbac` para detalles
  de esas áreas. Antes de tocar zonas sensibles —el pipeline MediatR, los interceptores EF,
  el modelo de autorización RBAC, el seeder o el esquema de BD— lee la memoria relevante Y verifica
  contra el código; si no coinciden, avisa antes de actuar.
- **Mantenimiento**: al cambiar el pipeline de behaviors, el esquema/entidades, el modelo de
  autorización, los interceptores EF o la estructura de DI, actualiza las memorias Serena y las
  reglas de `.claude/rules/` en el MISMO cambio.
- **Idioma**: los comentarios, logs y PR descriptions pueden ir en español o inglés; los
  identificadores de código siguen PascalCase/camelCase inglés (convención C# estándar).

---

## Scaffolding a new feature
```bash
/project:new-feature <AggregateName>
```
See `.claude/commands/new-feature.md` for full instructions.

---

## Environment variables

| Variable                                | Default                                  |
|-----------------------------------------|------------------------------------------|
| `ConnectionStrings__DefaultConnection`  | `Host=localhost;...Database=EnterpriseAppDb` |
| `ConnectionStrings__Redis`              | `localhost:6379`                         |
| `Jwt__Key`                              | ⚠️ Must override in production           |
| `Jwt__Issuer`                           | `EnterpriseApp`                          |
| `Jwt__Audience`                         | `EnterpriseApp.Clients`                  |
| `ASPNETCORE_ENVIRONMENT`                | `Development`                            |

---

## Running tests
```bash
dotnet test                          # all tests
dotnet test --filter "FullyQualifiedName~Domain"        # domain only
dotnet test --filter "FullyQualifiedName~Architecture"  # arch rules only
```
