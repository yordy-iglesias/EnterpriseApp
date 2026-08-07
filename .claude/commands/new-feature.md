# /project:new-feature — Scaffold a new Clean Architecture feature module

Scaffolds a complete vertical slice for a new aggregate in this solution.
Run in the solution root.

## Usage
```
/project:new-feature <AggregateName>
```
**Example:** `/project:new-feature Product`

## What gets created

```
src/EnterpriseApp.Domain/
  Entities/<AggregateName>.cs          ← AuditableEntity<TId> + strongly-typed Id
  ValueObjects/<AggregateName>Status.cs
  DomainEvents/<AggregateName>Events.cs
  Interfaces/Repositories/I<AggregateName>Repository.cs

src/EnterpriseApp.Application/
  Features/<AggregateName>s/
    DTOs/<AggregateName>Dto.cs
    Commands/Create<AggregateName>/Create<AggregateName>Command.cs
    Commands/Create<AggregateName>/Create<AggregateName>CommandHandler.cs
    Commands/Create<AggregateName>/Create<AggregateName>CommandValidator.cs
    Commands/Update<AggregateName>/...
    Commands/Delete<AggregateName>/...
    Queries/Get<AggregateName>ById/...
    Queries/GetPaged<AggregateName>s/...
  Common/Mappings/<AggregateName>MappingProfile.cs

src/EnterpriseApp.Infrastructure/
  Persistence/Configurations/<AggregateName>Configuration.cs
  Repositories/<AggregateName>Repository.cs

src/EnterpriseApp.API/
  Controllers/<AggregateName>sController.cs

tests/EnterpriseApp.Domain.Tests/
  Entities/<AggregateName>Tests.cs

tests/EnterpriseApp.Application.Tests/
  Features/<AggregateName>s/Create<AggregateName>CommandHandlerTests.cs
```

## Instructions for Claude

1. Follow every pattern in `TodoItem` exactly — same structure, same naming.
2. Use `DateTimeOffset` (not `DateTime`) for all temporal properties.
3. Strongly-typed ID: `public sealed record <AggregateName>Id(Guid Value)`.
4. Raise a `<AggregateName>CreatedEvent` in the factory method.
5. Add `I<AggregateName>Repository` to `IUnitOfWork` interface.
6. Register the repository in `InfrastructureServiceExtensions`.
7. Apply EF configuration with `HasConversion` for the strongly-typed ID.
8. Add global soft-delete query filter in `AppDbContext.OnModelCreating`.
9. After scaffolding, remind the developer to add an EF migration:
   ```
   dotnet ef migrations add Add<AggregateName> \
     --project src/EnterpriseApp.Infrastructure \
     --startup-project src/EnterpriseApp.API
   ```
