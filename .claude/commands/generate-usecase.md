# /project:generate-usecase

Genera un caso de uso completo siguiendo Clean Architecture.

## Uso
```
/project:generate-usecase <Entidad> <Operación> [tipo?]
```
- `Operación`: Create | Update | Delete | GetById | GetPaged | Custom
- `tipo`: Command (mutación) | Query (lectura) — se infiere de la operación si se omite

## Archivos generados

### Para un Command (ej: CreateProduct)
```
src/YourApp.Application/Products/Commands/CreateProduct/
├── CreateProductCommand.cs          # record con propiedades
├── CreateProductCommandHandler.cs   # implementa IRequestHandler
└── CreateProductCommandValidator.cs # FluentValidation

src/YourApp.Application/Products/   # si no existe
└── DTOs/
    └── ProductResponse.cs           # record de respuesta
```

### Para una Query (ej: GetProductById)
```
src/YourApp.Application/Products/Queries/GetProductById/
├── GetProductByIdQuery.cs
├── GetProductByIdQueryHandler.cs
└── ProductResponse.cs               # si no existe
```

### Interface de repositorio (si no existe)
```
src/YourApp.Domain/Repositories/
└── IProductRepository.cs
```

### Implementación de repositorio (si no existe)
```
src/YourApp.Infrastructure/Persistence/Repositories/
└── ProductRepository.cs
```

### Tests
```
tests/YourApp.UnitTests/Application/Products/Commands/
└── CreateProductCommandHandlerTests.cs
```

## Plantilla de Command generado
```csharp
namespace YourApp.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    Guid CategoryId) : IRequest<Result<ProductResponse>>;

internal sealed class CreateProductCommandHandler(
    IProductRepository repository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<CreateProductCommand, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(
        CreateProductCommand request, CancellationToken ct)
    {
        var categoryExists = await categoryRepository.ExistsAsync(request.CategoryId, ct);
        if (!categoryExists)
            return Result.Failure<ProductResponse>(CategoryErrors.NotFound);

        var product = Product.Create(request.Name, request.Description, request.Price, request.CategoryId);
        repository.Add(product);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(mapper.Map<ProductResponse>(product));
    }
}
```
