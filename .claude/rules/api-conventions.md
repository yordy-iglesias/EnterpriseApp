# Convenciones de API — ASP.NET Core 9

## Versionado
- Prefijo obligatorio `/api/v{n}` en todas las rutas
- Usar `Asp.Versioning.Mvc` / `Asp.Versioning.Http`
```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddApiExplorer(options => options.GroupNameFormat = "'v'VVV");
```

## Naming de endpoints (REST)
| Operación | Método | Ruta | Status success |
|---|---|---|---|
| Listar | GET | `/api/v1/products` | 200 |
| Obtener uno | GET | `/api/v1/products/{id}` | 200 / 404 |
| Crear | POST | `/api/v1/products` | 201 + `Location` |
| Actualizar completo | PUT | `/api/v1/products/{id}` | 200 / 204 |
| Actualizar parcial | PATCH | `/api/v1/products/{id}` | 200 / 204 |
| Eliminar | DELETE | `/api/v1/products/{id}` | 204 |
| Sub-recursos | `/api/v1/orders/{id}/items` | — | — |
| Acciones no-CRUD | POST | `/api/v1/orders/{id}/cancel` | 200 |

Reglas:
- Recursos en **plural** y **kebab-case**: `/api/v1/purchase-orders`
- IDs en segmentos de ruta, filtros/sorting en query string
- Sin verbos en rutas salvo acciones específicas

## Minimal API endpoint estándar
```csharp
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/products")
            .WithTags("Products")
            .HasApiVersion(1, 0)
            .RequireAuthorization();

        group.MapGet("/", GetAllAsync).WithName("GetProducts");
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetProductById");
        group.MapPost("/", CreateAsync).WithName("CreateProduct").RequireAuthorization("CanManageProducts");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateProduct").RequireAuthorization("CanManageProducts");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteProduct").RequireAuthorization("AdminOnly");

        return app;
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductByIdQuery(id), ct);
        return result.Match(
            product => Results.Ok(product),
            error => error.Code == ErrorCodes.NotFound
                ? Results.NotFound()
                : Results.Problem(error.Message));
    }
}
```

## Contratos — Request / Response

### DTOs con records
```csharp
// Request
public sealed record CreateProductRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(500)] string Description,
    [Required, Range(0.01, 999_999.99)] decimal Price,
    [Required] Guid CategoryId);

// Response
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string CategoryName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

### Paginación estándar
```csharp
public sealed record PagedQueryParams
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;
    public string? Search { get; init; }
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Data,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => PageNumber < TotalPages;
    public bool HasPrevious => PageNumber > 1;
}
```

## Manejo de errores — Problem Details (RFC 7807)
```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
    };
});

// Ejemplo de respuesta 400
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/products",
  "errors": {
    "Name": ["The Name field is required."],
    "Price": ["The field Price must be between 0.01 and 999999.99."]
  },
  "traceId": "00-abc...",
  "timestamp": "2026-04-16T10:00:00Z"
}
```

## Status codes — cuándo usar cuál
| Código | Uso |
|---|---|
| 200 OK | GET/PUT/PATCH exitoso con body |
| 201 Created | POST exitoso — añadir header `Location: /api/v1/products/{id}` |
| 202 Accepted | Operación asíncrona aceptada (background job) |
| 204 No Content | DELETE exitoso, PUT sin body |
| 400 Bad Request | Validación sintáctica fallida |
| 401 Unauthorized | Sin autenticación o token inválido |
| 403 Forbidden | Autenticado pero sin permisos |
| 404 Not Found | Recurso no existe |
| 409 Conflict | Estado actual incompatible (duplicado, concurrency) |
| 422 Unprocessable | Validación de negocio fallida |
| 429 Too Many Requests | Rate limit excedido |
| 500 Internal Error | Error no manejado — nunca exponer stack trace |
| 503 Service Unavailable | Dependencia externa caída (DB, Redis) |

## OpenAPI / Swagger
```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "YourApp API",
        Version = "v1",
        Description = "API para …",
        Contact = new OpenApiContact { Name = "Team", Email = "team@example.com" }
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "YourApp.Api.xml"));
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});
```

En cada endpoint, documentar:
```csharp
group.MapGet("/{id:guid}", GetByIdAsync)
    .WithSummary("Obtiene un producto por ID")
    .WithDescription("Retorna los datos completos del producto, incluyendo categoría.")
    .Produces<ProductResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);
```

## Idempotencia
- **GET, PUT, DELETE** son naturalmente idempotentes
- **POST** con header `Idempotency-Key` para operaciones monetarias/críticas
  - Guardar resultado en Redis con TTL de 24h por clave

## HATEOAS (opcional — según proyecto)
```json
{
  "id": "...",
  "name": "Product",
  "_links": {
    "self": { "href": "/api/v1/products/{id}" },
    "update": { "href": "/api/v1/products/{id}", "method": "PUT" },
    "delete": { "href": "/api/v1/products/{id}", "method": "DELETE" }
  }
}
```
