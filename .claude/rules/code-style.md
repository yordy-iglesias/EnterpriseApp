# Reglas de estilo de código — .NET 9 / C# 13

## Configuración del proyecto (.csproj)
```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisMode>All</AnalysisMode>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

## Naming
| Elemento | Convención | Ejemplo |
|---|---|---|
| Clase / Interface / Record | PascalCase | `UserService`, `IUserRepository` |
| Método | PascalCase | `GetUserByIdAsync` |
| Propiedad | PascalCase | `FirstName` |
| Campo privado | _camelCase | `_userRepository` |
| Parámetro / variable local | camelCase | `userId` |
| Constante | PascalCase | `MaxRetryCount` |
| Enum / sus valores | PascalCase | `UserStatus.Active` |
| Archivo | Mismo que clase | `UserService.cs` |

## C# 13 features obligatorias

### Primary constructors (donde aplique)
```csharp
// ✅ Correcto
public sealed class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    public async Task<Result<User>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // repository y logger disponibles como campos implícitos
    }
}
```

### Records para DTOs
```csharp
// ✅ Correcto — inmutable, value equality, deconstrucción
public sealed record CreateUserRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

public sealed record UserResponse(
    Guid Id, string FullName, string Email, DateTime CreatedAt);
```

### Pattern matching
```csharp
// ✅ Correcto
var message = user.Status switch
{
    UserStatus.Active => "Account is active",
    UserStatus.Suspended => "Account is suspended",
    UserStatus.Deleted => "Account has been deleted",
    _ => throw new UnreachableException()
};

// ✅ List patterns
if (results is [var single]) return Ok(single);
if (results is []) return NotFound();
```

### Collection expressions
```csharp
// ✅ C# 12+
List<string> roles = ["Admin", "Manager", "User"];
string[] headers = [ContentType, Authorization];
```

## Organización de archivos
- **Un tipo público por archivo** — sin excepciones
- **File-scoped namespaces**: `namespace YourApp.Application.Users;` (sin llaves)
- **Global usings** en `GlobalUsings.cs` de cada proyecto:
```csharp
global using MediatR;
global using FluentValidation;
global using Microsoft.EntityFrameworkCore;
```

## Async patterns
```csharp
// ✅ Siempre async hasta el final
public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
{
    var result = await _mediator.Send(new GetUserByIdQuery(id), ct);
    return result.Match(Ok, NotFound);
}

// ❌ Nunca bloquear
var user = _mediator.Send(query).Result; // Deadlock risk
```

## Result pattern (sin excepciones de negocio)
```csharp
// Usar ErrorOr<T> o Result<T> de la librería elegida
public async Task<Result<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
{
    if (await _repository.ExistsByEmailAsync(request.Email, ct))
        return Result.Failure<UserResponse>(UserErrors.EmailAlreadyExists);

    var user = User.Create(request.FirstName, request.LastName, request.Email);
    _repository.Add(user);
    await _unitOfWork.SaveChangesAsync(ct);
    return Result.Success(_mapper.Map<UserResponse>(user));
}
```
