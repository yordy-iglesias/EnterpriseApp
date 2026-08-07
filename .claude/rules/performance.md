# Reglas de Performance — ASP.NET Core 9

## EF Core — Queries optimizados

### AsNoTracking obligatorio en reads
```csharp
// ✅ Lecturas — sin tracking
var products = await _context.Products
    .AsNoTracking()
    .Where(p => p.IsActive && p.CategoryId == categoryId)
    .Select(p => new ProductSummary(p.Id, p.Name, p.Price)) // projection
    .ToListAsync(ct);

// ✅ Lecturas con relaciones
var orders = await _context.Orders
    .AsNoTrackingWithIdentityResolution()
    .Include(o => o.Items).ThenInclude(i => i.Product)
    .Where(o => o.UserId == userId)
    .ToListAsync(ct);

// ✅ Escrituras — con tracking
var product = await _context.Products.FindAsync([id], ct);
```

### Evitar N+1
```csharp
// ❌ N+1 — una query por cada orden
foreach (var order in orders)
{
    var items = await _context.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync();
}

// ✅ Un Include — una query con JOIN
var orders = await _context.Orders
    .Include(o => o.Items)
    .ToListAsync(ct);

// ✅ Split query para colecciones grandes
var orders = await _context.Orders
    .Include(o => o.Items).ThenInclude(i => i.Product)
    .AsSplitQuery()  // Evita producto cartesiano
    .ToListAsync(ct);
```

### Paginación eficiente
```csharp
// ✅ Keyset pagination (más eficiente que OFFSET para grandes datasets)
var products = await _context.Products
    .AsNoTracking()
    .Where(p => p.Id > lastSeenId)
    .OrderBy(p => p.Id)
    .Take(pageSize)
    .ToListAsync(ct);

// ✅ OFFSET pagination con índice
var products = await _context.Products
    .AsNoTracking()
    .OrderBy(p => p.CreatedAt)  // siempre ordenar por columna indexada
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(ct);
```

## Compilación y JIT
```xml
<!-- Para apps de alta performance -->
<PublishReadyToRun>true</PublishReadyToRun>  <!-- AOT parcial -->
<TieredCompilation>true</TieredCompilation>  <!-- Default en .NET 9 -->
```

## Response compression
```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o =>
    o.Level = CompressionLevel.Fastest);
```

## Minimal API vs Controllers
Para endpoints de alta frecuencia, preferir Minimal API (menor overhead):
```csharp
app.MapGet("/api/v1/products/{id:guid}", async (
    Guid id, ISender mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetProductByIdQuery(id), ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
})
.WithName("GetProduct")
.WithTags("Products")
.RequireAuthorization();
```

## Benchmarking con BenchmarkDotNet
```csharp
// Para métodos críticos de performance
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ProductQueryBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<List<Product>> GetWithTracking() => ...;

    [Benchmark]
    public async Task<List<Product>> GetWithNoTracking() => ...;

    [Benchmark]
    public async Task<List<ProductSummary>> GetWithProjection() => ...;
}
```

## Objetivos de performance API
| Percentil | Latencia objetivo |
|---|---|
| P50 | < 50ms |
| P95 | < 200ms |
| P99 | < 500ms |
| Max | < 2s |
| Throughput | > 1000 req/s (single instance) |
