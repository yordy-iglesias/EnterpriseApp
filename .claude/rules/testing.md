# Reglas de Testing — .NET 9

## Stack
- **xUnit 2**: framework de tests
- **Moq 4**: mocks de interfaces
- **FluentAssertions 6**: assertions legibles
- **Testcontainers**: contenedores reales para integration tests
- **Bogus**: generación de datos de prueba (faker)
- **ArchUnitNET**: tests de arquitectura

## Proyectos de test
```
tests/
├── YourApp.UnitTests/          # Tests unitarios puros (sin I/O)
├── YourApp.IntegrationTests/   # Tests con DB, Redis, etc. reales (Testcontainers)
└── YourApp.ArchitectureTests/  # Validaciones de arquitectura
```

## Convenciones de naming
```csharp
// Patrón: {UnitUnderTest}_{Scenario}_{ExpectedResult}
public async Task GetUserByIdAsync_UserExists_ReturnsUser()
public async Task GetUserByIdAsync_UserNotFound_ReturnsFailureResult()
public async Task CreateUserAsync_EmailAlreadyExists_ReturnsConflictError()
```

## Unit Test — patrón estándar
```csharp
public sealed class CreateProductCommandHandlerTests
{
    // Arrange: setup compartido
    private readonly Mock<IProductRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly IMapper _mapper = CreateMapper();
    private readonly CreateProductCommandHandler _sut;

    public CreateProductCommandHandlerTests()
    {
        _sut = new CreateProductCommandHandler(_repositoryMock.Object, _uowMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateProductAndReturnSuccess()
    {
        // Arrange
        var command = new Faker<CreateProductCommand>()
            .CustomInstantiator(f => new(f.Commerce.ProductName(), f.Lorem.Sentence(), f.Random.Decimal(1, 1000), Guid.NewGuid()))
            .Generate();
        _uowMock.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(command.Name);
        _repositoryMock.Verify(x => x.Add(It.Is<Product>(p => p.Name == command.Name)), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }
}
```

## Integration Test con Testcontainers
```csharp
[Collection("Integration")]
public sealed class ProductsApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    [Fact]
    public async Task POST_api_products_ValidBody_Returns201WithProduct()
    {
        // Arrange
        var client = factory.CreateAuthenticatedClient("Admin");
        var body = new { Name = "Test Product", Description = "Desc", Price = 99.99m };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/products", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        product.Should().NotBeNull();
        product!.Name.Should().Be("Test Product");
    }
}

// Factory con SQL Server real via Testcontainers
public sealed class CustomWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        // Aplica migraciones automáticamente
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveDbContext<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlServer(_sqlContainer.GetConnectionString()));
        });
    }

    public new async Task DisposeAsync() => await _sqlContainer.DisposeAsync();
}
```

## Architecture Tests
```csharp
public sealed class ArchitectureTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            Assembly.Load("YourApp.Domain"),
            Assembly.Load("YourApp.Application"),
            Assembly.Load("YourApp.Infrastructure"))
        .Build();

    [Fact]
    public void Domain_ShouldNot_DependOnApplication()
    {
        Classes().That().ResideInNamespace("YourApp.Domain")
            .Should().NotDependOnAny(Classes().That().ResideInNamespace("YourApp.Application"))
            .Check(Architecture);
    }

    [Fact]
    public void Handlers_Should_BeInternal()
    {
        Classes().That().HaveNameEndingWith("Handler")
            .Should().NotBePublic()
            .Check(Architecture);
    }
}
```

## Umbrales mínimos
```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverageOutputFormat>cobertura</CoverageOutputFormat>
  <Threshold>80</Threshold>
  <ThresholdType>line,branch,method</ThresholdType>
  <ThresholdStat>minimum</ThresholdStat>
</PropertyGroup>
```
