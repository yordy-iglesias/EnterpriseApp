# /project:test

Genera o completa tests para el código indicado.

## Uso
```
/project:test <Archivo.cs> [tipo?]
```
Tipos: `unit` | `integration` | `architecture`

## Unit Tests (xUnit + Moq + FluentAssertions)

### Estructura estándar (AAA)
```csharp
public sealed class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _handler = new CreateProductCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            MapsterMapper.CreateMapper());
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        var command = new CreateProductCommand("Test Product", "Description", 99.99m, Guid.NewGuid());
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Product");
        _repositoryMock.Verify(x => x.Add(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ReturnsFailure()
    {
        // Arrange & Act & Assert
        // ...
    }
}
```

## Integration Tests (Testcontainers + WebApplicationFactory)
```csharp
public class ProductsEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task POST_CreateProduct_Returns201()
    {
        // Arrange
        var client = factory.CreateClient();
        var command = new { Name = "Test", Description = "Desc", Price = 9.99 };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        product!.Name.Should().Be("Test");
    }
}

// CustomWebApplicationFactory usa Testcontainers para SQL Server real
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();
    // ...
}
```

## Architecture Tests (ArchUnitNET)
```csharp
[Fact]
public void DomainLayer_ShouldNotDependOnInfrastructure()
{
    var rule = Classes().That().ResideInNamespace("*.Domain.*")
        .Should().NotDependOnAny(Classes().That().ResideInNamespace("*.Infrastructure.*"));
    rule.Check(Architecture);
}
```

## Ejecutar tests
```bash
dotnet test                                    # todos
dotnet test --filter "Category=Unit"           # solo unit
dotnet test --filter "Category=Integration"    # solo integration
dotnet test --collect:"XPlat Code Coverage"    # con cobertura
reportgenerator -reports:coverage.xml -targetdir:coveragereport
```
