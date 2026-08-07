# Integration Testing — WebApplicationFactory y Testcontainers

> Notas durables sobre el setup de tests de integración en este template.

## Setup actual

Los tests de integración usan `WebApplicationFactory<Program>` + Testcontainers para una BD real.
El proyecto de tests de integración aún no existe — esta memoria documenta el patrón a seguir
cuando se agregue, basado en `.claude/rules/testing.md`.

## Patrón `CustomWebAppFactory`

```csharp
public sealed class CustomWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Cambiar a PostgreSqlContainer si se usa PostgreSQL
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Reemplazar DbContext con el de Testcontainers
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlServer(_sql.GetConnectionString()));

            // Reemplazar Redis con in-memory para tests
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }

    public async Task InitializeAsync() => await _sql.StartAsync();
    public new async Task DisposeAsync() => await _sql.DisposeAsync();
}
```

## Autenticación en tests

```csharp
// Helper para crear cliente autenticado con permisos específicos
public HttpClient CreateClientWithPermissions(params string[] permissions)
{
    var client = CreateClient();
    // Configurar JWT de test con los permisos necesarios
    var token = JwtTestHelper.GenerateToken(permissions);
    client.DefaultRequestHeaders.Authorization = new("Bearer", token);
    return client;
}
```

## Colección de tests (evitar conflictos de base de datos)

```csharp
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<CustomWebAppFactory> { }

[Collection("Integration")]
public sealed class TodosApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    [Fact]
    public async Task POST_api_todos_ValidBody_Returns201()
    {
        var client = factory.CreateClientWithPermissions("todo.create");
        var response = await client.PostAsJsonAsync("/api/todos", new { Title = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```
