using System.Text.Json;
using EnterpriseApp.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace EnterpriseApp.Infrastructure.Caching;

/// <summary>
/// HybridCache (.NET 9) implementation of <see cref="ICacheService"/>.
/// HybridCache uses an in-process L1 cache (IMemoryCache) in front of a
/// distributed L2 store (Redis).  Single objects are serialised to JSON.
/// </summary>
public sealed class RedisCacheService(HybridCache hybridCache) : ICacheService
{
    private static readonly HybridCacheEntryOptions DefaultOptions = new()
    {
        Expiration        = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        // GetOrCreateAsync with a factory that returns null simulates a pure "get".
        // If the key is absent, null is cached briefly to prevent stampedes.
        return await hybridCache.GetOrCreateAsync<T?>(
            key,
            _ => ValueTask.FromResult<T?>(null),
            DefaultOptions,
            cancellationToken: ct);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiry = null, CancellationToken ct = default)
        where T : class
    {
        var options = absoluteExpiry.HasValue
            ? new HybridCacheEntryOptions { Expiration = absoluteExpiry.Value }
            : DefaultOptions;

        await hybridCache.SetAsync(key, value, options, cancellationToken: ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await hybridCache.RemoveAsync(key, ct);

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) =>
        await hybridCache.RemoveByTagAsync(prefix, ct);
}
