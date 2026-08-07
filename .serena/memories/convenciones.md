# convenciones (patrones, recetas, comandos, gotchas)

> Detalle operativo. El resumen está en `CLAUDE.md` y en `.claude/rules/`; aquí van las
> recetas concretas para tareas frecuentes y los gotchas del entorno.

## Receta: añadir un nuevo feature CQRS

1. **Domain**: crear entidad en `Domain/Entities/`, value objects en `Domain/ValueObjects/`,
   eventos en `Domain/DomainEvents/`, interfaces de repo en `Domain/Interfaces/Repositories/`.
2. **Application/Features/{Feature}/**:
   - `Commands/{Operation}{Entity}/` → `{Op}{Entity}Command.cs` + `{Op}{Entity}CommandHandler.cs`
     + `{Op}{Entity}CommandValidator.cs` (uno por comando).
   - `Queries/Get{Entity}By{X}/` → `Get{Entity}By{X}Query.cs` + handler + `{Entity}Response.cs`.
   - `EventHandlers/` → handlers de domain events si aplica.
   - DTOs en `DTOs/`.
3. **Infrastructure**:
   - Entidad EF: configuración en `Persistence/Configurations/{Feature}/{Entity}Configuration.cs`.
   - Repositorio: implementación en `Repositories/{Feature}/{Entity}Repository.cs`.
   - Registrar en `InfrastructureServiceExtensions.cs` (`services.AddScoped<IFooRepo, FooRepo>()`).
4. **API**:
   - Controlador en `Controllers/{Feature}Controller.cs` derivando de `ControllerBase`.
   - Decorar con `[RequirePermission("feature.action")]` según `.claude/rules/authorization.md`.
5. Ejecutar `dotnet test` para verificar que las architecture tests siguen pasando.

## Receta: añadir un repositorio

1. Interfaz en `Domain/Interfaces/Repositories/I{Entity}Repository.cs`.
2. Implementación en `Infrastructure/Repositories/{Entity}Repository.cs` usando `AppDbContext`
   via `IApplicationDbContext`.
3. Registrar en `InfrastructureServiceExtensions`:
   ```csharp
   services.AddScoped<I{Entity}Repository, {Entity}Repository>();
   ```
4. Si el `DbSet` no existe aún, añadirlo en `AppDbContext` y crear `IEntityTypeConfiguration<T>`.
5. Generar la migración (ver comandos abajo).

## Receta: handler CQRS estándar

```csharp
// Command handler
internal sealed class Create{Entity}CommandHandler(
    IApplicationDbContext db,
    IMapper mapper) : IRequestHandler<Create{Entity}Command, Guid>
{
    public async Task<Guid> Handle(Create{Entity}Command request, CancellationToken ct)
    {
        var entity = {Entity}.Create(request.Name, ...);
        db.{Entities}.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id.Value;
    }
}

// Query handler (AsNoTracking obligatorio)
internal sealed class Get{Entity}ByIdQueryHandler(IApplicationDbContext db, IMapper mapper)
    : IRequestHandler<Get{Entity}ByIdQuery, {Entity}Response?>
{
    public async Task<{Entity}Response?> Handle(Get{Entity}ByIdQuery request, CancellationToken ct)
        => await db.{Entities}
            .AsNoTracking()
            .Where(e => e.Id == {EntityId}.From(request.Id))
            .ProjectTo<{Entity}Response>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
}
```

## Receta: query cacheable

```csharp
public sealed record Get{Entity}ByIdQuery(Guid Id) : IRequest<{Entity}Response?>, ICachedQuery<{Entity}Response?>
{
    public string CacheKey => $"{entity}:{Id}";
    public TimeSpan? Expiration => TimeSpan.FromMinutes(5);
}
```

## Comandos de build y test

```bash
# Build
dotnet build EnterpriseApp.sln

# Todos los tests
dotnet test

# Solo tests de dominio
dotnet test --filter "FullyQualifiedName~Domain"

# Solo architecture tests
dotnet test --filter "FullyQualifiedName~Architecture"

# Migraciones (PostgreSQL — proveedor default)
dotnet ef migrations add <Nombre> \
  --project src/EnterpriseApp.Infrastructure \
  --startup-project src/EnterpriseApp.API

# Migraciones SQL Server
$env:Database__Provider="SqlServer"; dotnet ef migrations add <Nombre> \
  --project src/EnterpriseApp.Infrastructure \
  --startup-project src/EnterpriseApp.API

# Aplicar migraciones manualmente
dotnet ef database update \
  --project src/EnterpriseApp.Infrastructure \
  --startup-project src/EnterpriseApp.API
```

## Arrancar en desarrollo

```bash
# SQLite — sin Docker
bash setup.sh --provider sqlite
dotnet run --project src/EnterpriseApp.API

# PostgreSQL + Redis — requiere Docker
docker compose up -d
bash setup.sh --provider postgres
dotnet run --project src/EnterpriseApp.API
# Scalar UI → https://localhost:5001/scalar/v1
# Health    → https://localhost:5001/health
```

## Estilo C# 13 / .NET 9

- Primary constructors donde aplique.
- Records para DTOs/commands/queries (inmutables, value equality).
- File-scoped namespaces: `namespace EnterpriseApp.X.Y;` (sin llaves).
- `sealed` en clases que no se heredan (handlers, validators, controllers).
- `internal sealed` en handlers (no son API pública del ensamblado).
- Pattern matching y collection expressions.
- `async/await` hasta el final — nunca `.Result` ni `.Wait()`.
- `AsNoTracking()` en todas las queries de lectura.
- `Result<T>` para fallos esperados de negocio; `DomainException` para invariants violadas.
- `ProblemDetails` RFC 7807 para todos los errores HTTP vía `GlobalExceptionMiddleware`.

## Gotchas conocidos

- **`HasQueryFilter`**: solo `TodoItem` lo tiene por ahora. Al agregar un aggregate con soft-delete,
  añadir el filtro en `OnModelCreating` o las queries incluirán registros eliminados.
- **Interceptores null-safe**: `AppDbContext` acepta `null` en interceptores por el factory design-time.
  En runtime siempre se pasan; en `AppDbContextFactory` se crea sin DI (pasan null).
- **Redis opcional**: si `ConnectionStrings:Redis` está vacío o el proveedor es SQLite, el sistema
  cae a `IMemoryCache` en-proceso. El código de `InfrastructureServiceExtensions` lo maneja
  automáticamente; no asumir que Redis siempre está disponible.
- **`PermissionAuditLog` es append-only**: el repositorio solo expone `AddAsync` y lecturas.
  No añadir métodos de actualización sin revisar los requerimientos de cumplimiento (SOC2/ISO 27001).
- **Permisos de sistema**: roles y permisos con `IsSystem=true` no pueden borrarse ni renombrarse
  (lanza `DomainException`). El seed los crea al arrancar.
- **JWT sin refresh token**: la plantilla tiene auth JWT pero no implementa refresh tokens todavía.

Ver [[arquitectura]], [[modulos]], [[esquema-db]].
