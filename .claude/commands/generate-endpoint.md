# /project:generate-endpoint

Genera un endpoint REST completo con toda la vertical de código necesaria.

## Uso
```
/project:generate-endpoint <Entidad> <Operación>
```
- `Entidad`: nombre en PascalCase (ej: Product)
- `Operación`: Create | Update | Delete | GetById | GetAll | Custom

## Lo que genera

### 1. Endpoint / Controller
```csharp
// Controllers/ProductsController.cs
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class ProductsController(ISender mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateProductCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.ToProblemDetails();
    }
}
```

### 2. Command/Query + Handler (MediatR)
```csharp
// Application/Products/Commands/CreateProduct/
// CreateProductCommand.cs
public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    Guid CategoryId) : IRequest<Result<ProductResponse>>;

// CreateProductCommandHandler.cs
internal sealed class CreateProductCommandHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<CreateProductCommand, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(
        CreateProductCommand request,
        CancellationToken ct)
    {
        // Validar que la categoría existe, crear entidad, persistir
        var product = Product.Create(request.Name, request.Description, request.Price);
        repository.Add(product);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(mapper.Map<ProductResponse>(product));
    }
}
```

### 3. FluentValidation Validator
```csharp
// CreateProductCommandValidator.cs
internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(999999.99m);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}
```

### 4. Response DTO (record)
```csharp
public sealed record ProductResponse(
    Guid Id, string Name, string Description, decimal Price,
    DateTime CreatedAt, DateTime? UpdatedAt);
```

### 5. Tests unitarios del Handler
- Happy path con mock del repositorio
- Caso de entidad no encontrada (404)
- Caso de validación fallida (422)
