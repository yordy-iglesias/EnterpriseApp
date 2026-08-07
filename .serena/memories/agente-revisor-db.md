# Notas del revisor de acceso a datos — hallazgos durables

## Uso correcto de IDs fuertemente tipados en repositorios

Los IDs (`TodoId`, `RoleId`, `PermissionId`, etc.) son `record struct` wrappers de `Guid`.
EF Core los convierte via `HasConversion` en cada `IEntityTypeConfiguration<T>`.

**Regla**: en queries LINQ usa siempre el tipo wrapeado, no el Guid crudo:
```csharp
// ✅ Correcto
db.TodoItems.Where(t => t.Id == TodoId.From(request.Id))

// ❌ Incorrecto — puede fallar si la conversión no se aplica al comparar
db.TodoItems.Where(t => t.Id.Value == request.Id)
```

Si EF no resuelve correctamente el ID en un predicado, verificar que `HasConversion` está
registrado en `IEntityTypeConfiguration<T>` para ese campo, no solo para el PK.

## Soft-delete y QueryFilter

`HasQueryFilter(e => !e.IsDeleted)` se aplica solo a entidades que lo tienen configurado
en `AppDbContext.OnModelCreating`. Entidades SIN este filtro devolverán registros
marcados como eliminados si el campo `IsDeleted` existe.

Para ignorar el filtro intencionalmente (e.g., admin viendo todos los registros):
```csharp
db.TodoItems.IgnoreQueryFilters().Where(...)
```

## AsNoTracking en lectura — obligatorio

Toda query de lectura debe usar `AsNoTracking()` o `AsNoTrackingWithIdentityResolution()`.
Si falta, EF trackea las entidades innecesariamente → overhead de memoria y riesgo de
accidentalmente persistir cambios no intencionados.

## Cómo verificar una colisión de namespace

Si un handler importa tanto `EnterpriseApp.Domain.Entities` como `EnterpriseApp.Application.Features.X.DTOs`
y ambos tienen un tipo `{Entity}Response`, el compilador lanzará CS0104.
Verificar con `find_symbol` o grep antes de reportarlo como necesario:
```bash
grep -r "class TodoResponse" src/
```
Si aparece en múltiples namespaces, calificar completamente en los puntos de ambigüedad.
