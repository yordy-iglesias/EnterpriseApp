# SKILL: Generate Endpoint — Auto-invocado

## Trigger
Se activa cuando el usuario dice:
- "crea un endpoint para X"
- "necesito la API de X"
- "genera el CRUD de X"
- "añade operación X a la entidad Y"

## Pasos del workflow

### 1. Identificar tipo de operación
Preguntar si no es claro:
- ¿Es Command (mutación) o Query (lectura)?
- ¿Qué entidad?
- ¿Qué reglas de negocio aplican?

### 2. Crear Response DTO
```csharp
// Application/{Entity}/DTOs/{Entity}Response.cs
public sealed record {Entity}Response(...);
```

### 3. Crear Command o Query + Validator
Si es Command:
```
Application/{Entity}/Commands/{Operation}{Entity}/
├── {Operation}{Entity}Command.cs      (record con propiedades)
├── {Operation}{Entity}CommandHandler.cs
└── {Operation}{Entity}CommandValidator.cs
```
Si es Query:
```
Application/{Entity}/Queries/Get{Entity}ById/
├── Get{Entity}ByIdQuery.cs
└── Get{Entity}ByIdQueryHandler.cs
```

### 4. Crear o actualizar Controller/Endpoint
Añadir acción con atributos correctos:
- `[HttpGet/Post/Put/Patch/Delete]`
- `[ProducesResponseType]` para cada código de respuesta posible
- `[Authorize]` o policy apropiada
- `[EnableRateLimiting]` si es endpoint público

### 5. Registrar en DI (si hay nuevos servicios)
En `DependencyInjection.cs` del proyecto correspondiente

### 6. Generar tests unitarios del Handler
- Happy path
- Not found / conflict / validation error
- CancellationToken pasado correctamente

### 7. Generar test de integración del endpoint
- Request válido → respuesta correcta
- Request inválido → 422 con detalles
- No autenticado → 401
- Sin permisos → 403

### 8. Verificar compilación
```bash
dotnet build --no-incremental -warnaserror
dotnet test --filter "Category=Unit" --no-build
```
