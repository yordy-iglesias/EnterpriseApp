# /project:review

Revisión de código completa del diff actual.

## Checklist .NET / C# 13

### C# y .NET
- [ ] Primary constructors usados donde aplique
- [ ] Record types para DTOs y Value Objects
- [ ] Nullable reference types sin suprimir con `!` innecesariamente
- [ ] `CancellationToken` en todos los métodos async
- [ ] Sin `.Result`, `.Wait()` o `async void` (excepto event handlers)
- [ ] `using` declarativo (no bloques) para IDisposable
- [ ] Pattern matching preferido sobre if/else chains
- [ ] `required` en propiedades obligatorias

### ASP.NET Core
- [ ] `[ProducesResponseType]` en todos los endpoints
- [ ] Problem Details para errores (RFC 7807)
- [ ] Versioning de API configurado
- [ ] Rate limiting en endpoints públicos
- [ ] Autorización adecuada ([Authorize], policies)
- [ ] Binding de parámetros explícito ([FromBody], [FromRoute], etc.)

### Entity Framework Core
- [ ] Sin N+1 queries (usar Include() apropiado o projection)
- [ ] `AsNoTracking()` en todas las queries de solo lectura
- [ ] `AsNoTrackingWithIdentityResolution()` para colecciones con relaciones
- [ ] Índices definidos en Fluent API para columnas de búsqueda frecuente
- [ ] Migraciones con nombres descriptivos
- [ ] Sin `SaveChanges()` fuera del Unit of Work

### Arquitectura
- [ ] Dependencias apuntan hacia el interior (Domain ← Application ← Infrastructure)
- [ ] Sin lógica de negocio en Controllers/Endpoints
- [ ] Sin acceso a DbContext fuera de Infrastructure
- [ ] Interfaces de repositorio en Domain/Application
- [ ] DTOs no exponen entidades de dominio directamente

### Caché
- [ ] Queries frecuentes de lectura usando IDistributedCache/IMemoryCache
- [ ] TTL apropiado según volatilidad del dato
- [ ] Invalidación de caché en mutaciones

### Seguridad
- [ ] Sin secrets hardcoded
- [ ] SQL parameterizado (EF lo hace automáticamente, verificar Dapper/raw SQL)
- [ ] Autorización basada en recursos donde aplique
- [ ] Datos sensibles no logueados

### Tests
- [ ] Cobertura ≥ 80% en handlers nuevos
- [ ] Tests de integración para endpoints nuevos
- [ ] Mocks de repositorios con Moq (no mocks de DbContext directamente)

## Formato de output
```
[ERROR|WARNING|INFO] Archivo.cs:Línea — Descripción
→ Fix sugerido con código
```
