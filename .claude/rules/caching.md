# Reglas de Caché — .NET 9

## Estrategias disponibles

### 1. IMemoryCache — caché en proceso
```csharp
// Para datos que no se comparten entre instancias
// Ideal: configuración estática, catálogos pequeños, sesiones de usuario

builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // límite de entradas
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
});

// Uso con GetOrCreateAsync
public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct)
{
    return await _cache.GetOrCreateAsync(
        CacheKeys.AllCategories,
        async entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
            entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            entry.SetSize(1);
            entry.SetPriority(CacheItemPriority.High);
            return await _repository.GetAllAsync(ct);
        }) ?? [];
}
```

### 2. IDistributedCache (Redis) — caché distribuida
```csharp
// Para datos compartidos entre múltiples instancias
// Ideal: tokens, resultados de búsqueda, datos de usuario frecuentes

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "YourApp:";
});

// Wrapper tipado recomendado
public sealed class RedisCacheService(IDistributedCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is null) return default;
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        await cache.SetAsync(key, bytes, options, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => cache.RemoveAsync(key, ct);
}
```

### 3. Output Cache (respuestas HTTP) — .NET 7+
```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(30)));
    options.AddPolicy("Products", b => b
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("products")
        .VaryByQuery("pageNumber", "pageSize", "search"));
});

// En el endpoint
[HttpGet]
[OutputCache(PolicyName = "Products")]
public async Task<IActionResult> GetProducts(...) { }

// Invalidar por tag cuando hay mutación
await _outputCacheStore.EvictByTagAsync("products", ct);
```

### 4. HybridCache — .NET 9 (recomendado)
```csharp
// Combina L1 (in-memory) + L2 (distributed) automáticamente
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024; // 1MB
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

// Uso
public async Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken ct)
{
    return await _hybridCache.GetOrCreateAsync(
        $"user:{userId}",
        async _ => await _repository.GetByIdAsync(userId, ct),
        cancellationToken: ct);
}
```

## Cache Keys — convención
```csharp
public static class CacheKeys
{
    public static string User(Guid id) => $"user:{id}";
    public static string UserList(int page, int size) => $"users:list:{page}:{size}";
    public static string Product(Guid id) => $"product:{id}";
    public const string AllCategories = "categories:all";
}
```

## Reglas de invalidación
- **Mutaciones** (Create/Update/Delete) deben invalidar caché relacionado
- Usar eventos de dominio para disparar invalidación desacoplada
- Preferir TTL cortos (< 5min) sobre invalidación explícita para datos volátiles
- **Nunca** cachear datos sensibles (passwords, tokens, datos financieros)

## TTL por tipo de dato
| Tipo de dato | TTL recomendado |
|---|---|
| Configuración estática | 1 hora |
| Catálogos (categorías, países) | 30 minutos |
| Datos de usuario | 5-15 minutos |
| Resultados de búsqueda | 1-2 minutos |
| Datos en tiempo real | No cachear |
