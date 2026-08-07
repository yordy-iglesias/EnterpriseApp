# Clean Architecture — Reglas de Arquitectura

## Estructura de capas
```
src/
├── YourApp.Domain/              # Núcleo — sin dependencias externas
│   ├── Entities/                # Aggregate Roots y Entities
│   ├── ValueObjects/            # Objetos de valor inmutables
│   ├── Enums/
│   ├── Errors/                  # Domain errors (constantes tipadas)
│   ├── Events/                  # Domain Events (records)
│   ├── Repositories/            # Interfaces de repositorios (solo interfaces)
│   └── Services/                # Domain Services (lógica que no pertenece a una entidad)
│
├── YourApp.Application/         # Casos de uso — depende solo de Domain
│   ├── Common/
│   │   ├── Behaviors/           # MediatR pipeline behaviors
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── CachingBehavior.cs
│   │   │   └── TransactionBehavior.cs
│   │   ├── Interfaces/          # Interfaces de infraestructura (email, storage, etc.)
│   │   └── Models/              # Pagination, Result, etc.
│   └── {Feature}/
│       ├── Commands/
│       │   └── {Operation}{Entity}/
│       │       ├── {Operation}{Entity}Command.cs
│       │       ├── {Operation}{Entity}CommandHandler.cs
│       │       └── {Operation}{Entity}CommandValidator.cs
│       ├── Queries/
│       │   └── Get{Entity}By{X}/
│       │       ├── Get{Entity}By{X}Query.cs
│       │       ├── Get{Entity}By{X}QueryHandler.cs
│       │       └── {Entity}Response.cs
│       └── EventHandlers/       # Handlers de Domain Events
│
├── YourApp.Infrastructure/      # Implementaciones concretas — depende de Application
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   ├── Configurations/      # Fluent API EF Core
│   │   ├── Repositories/        # Implementaciones de IRepository
│   │   ├── Migrations/
│   │   └── Specifications/      # Specification pattern
│   ├── Services/                # Email, Storage, PDF, etc.
│   ├── Messaging/               # MassTransit consumers y publishers
│   └── DependencyInjection.cs
│
└── YourApp.Api/                 # Entry point — depende de Application + Infrastructure
    ├── Controllers/
    ├── Middleware/
    ├── Program.cs
    └── DependencyInjection.cs
```

## Regla de Dependencia (CRÍTICA)
```
Domain ← Application ← Infrastructure
                     ← Api
```
- **Domain**: NO importa ningún paquete excepto `System.*`
- **Application**: importa `Domain`, `MediatR`, `FluentValidation` — NO `EntityFramework`
- **Infrastructure**: implementa interfaces de `Domain` y `Application`
- **Api**: referencia `Application` e `Infrastructure` solo para DI

### Validado por ArchUnitNET (tests/ArchitectureTests)
```csharp
[Fact]
public void Domain_MustNotDependOnApplication()
{
    Classes().That().ResideInNamespace("*.Domain.*")
        .Should().NotDependOnAny(
            Classes().That().ResideInNamespace("*.Application.*"))
        .Check(Architecture);
}
```

## Pipeline de MediatR (orden de behaviors)
```
Request → LoggingBehavior → ValidationBehavior → CachingBehavior → TransactionBehavior → Handler
```

```csharp
// ValidationBehavior — rechaza antes de llegar al handler
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Specification Pattern
```csharp
// Domain/Specifications/Base
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    public bool IsSatisfiedBy(T entity) => ToExpression().Compile()(entity);
}

// Ejemplo
public sealed class ActiveProductsInCategory(Guid categoryId) : Specification<Product>
{
    public override Expression<Func<Product, bool>> ToExpression()
        => p => p.IsActive && p.CategoryId == categoryId;
}

// Uso en repositorio
var products = await _repository.FindAsync(new ActiveProductsInCategory(categoryId), ct);
```

## Entidad base recomendada
```csharp
// Domain/Common/AggregateRoot.cs
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```
