# /project:fix-issue

Diagnostica y corrige un bug o issue reportado en el proyecto .NET.

## Uso
```
/project:fix-issue [número-issue | descripción]
```
Ejemplos:
- `/project:fix-issue #312`
- `/project:fix-issue "el endpoint POST /api/v1/orders devuelve 500 cuando el cliente no tiene dirección"`
- `/project:fix-issue "EF Core lanza N+1 en GetOrdersByUserQuery"`

## Flujo de trabajo

### 1. Reproducción del problema
- Leer el issue (GitHub/Jira) o descripción del usuario
- Identificar el proyecto afectado: `Api`, `Application`, `Domain`, `Infrastructure`, `Worker`
- Reproducir localmente: `dotnet run --project src/YourApp.Api` + llamada al endpoint
- Capturar la excepción/stack trace exacto o el comportamiento observado

### 2. Diagnóstico — causa raíz
- [ ] ¿Es **DDD / invariante de dominio** violado? Revisar agregados y validaciones
- [ ] ¿Es **EF Core**? N+1, tracking, migraciones, tipos incorrectos
- [ ] ¿Es **validación**? FluentValidation, DataAnnotations, pipelines MediatR
- [ ] ¿Es **concurrencia**? Race conditions, `ConcurrencyToken`, locking
- [ ] ¿Es **async/await**? Deadlocks, `.Result`, `Task.Run` innecesario
- [ ] ¿Es **serialización**? System.Text.Json converters, polymorphism
- [ ] ¿Es **autenticación/autorización**? Token, policies, claims
- [ ] ¿Es **configuración**? `appsettings.json`, `IOptions<T>`, secretos faltantes
- [ ] ¿Es **observabilidad**? Logs insuficientes — añadir `ILogger` antes de fix

### 3. Test que reproduce el bug
Antes del fix, escribir un test que **falle**:
```csharp
[Fact]
public async Task Handle_OrderWithoutAddress_ShouldReturnValidationError()
{
    // Arrange
    var customer = CustomerFaker.Generate() with { Address = null };
    await _db.Customers.AddAsync(customer);
    await _db.SaveChangesAsync();
    var command = new CreateOrderCommand(customer.Id, [new OrderItem(...)]);

    // Act
    var result = await _sut.Handle(command, default);

    // Assert — debe fallar antes del fix
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(OrderErrors.CustomerAddressMissing.Code);
}
```

### 4. Fix mínimo
- Cambio **más pequeño** posible que resuelva la causa raíz
- Respetar capas de Clean Architecture / Hexagonal / DDD del proyecto
- Usar Result pattern — no lanzar excepciones de negocio nuevas
- No refactorizar código no relacionado en el mismo commit
- Si el fix requiere migración EF Core:
  ```bash
  dotnet ef migrations add FixForIssue{N} --project src/YourApp.Infrastructure --startup-project src/YourApp.Api
  dotnet ef database update --project src/YourApp.Infrastructure --startup-project src/YourApp.Api
  ```

### 5. Validación
```bash
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```
Todos deben pasar antes de continuar.

### 6. Mensaje de commit
```
fix(scope): descripción concisa del bug arreglado

Refs #<número-issue>

- Causa raíz: <explicación técnica breve>
- Solución: <qué cambió y por qué>
- Test añadido: <nombre del test>
- Migración: <sí/no — nombre si aplica>
```

## Formato de output del comando
```markdown
## Fix: <título del issue>

### Causa raíz
<explicación del bug — capa afectada y por qué ocurre>

### Archivos modificados
- `src/YourApp.Application/.../Handler.cs` — <qué cambió>
- `src/YourApp.Domain/.../AggregateRoot.cs` — <qué cambió>

### Test añadido
- `tests/YourApp.UnitTests/.../HandlerTests.cs` — <caso cubierto>

### Migración EF Core
- <nombre> o "No requerida"

### Comandos ejecutados
- ✅ `dotnet format`
- ✅ `dotnet build -warnaserror`
- ✅ Unit tests (X pasaron, 0 fallaron)
- ✅ Integration tests (Y pasaron, 0 fallaron)

### Riesgos / notas
<regresiones posibles, follow-ups, deuda técnica detectada>
```
