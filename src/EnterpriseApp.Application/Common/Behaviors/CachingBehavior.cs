using EnterpriseApp.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that provides automatic caching for queries
/// that implement <see cref="ICachedQuery{TResponse}"/>.
/// <para>
/// Execution flow:
/// <list type="number">
///   <item>Look up the cache using <see cref="ICachedQuery{TResponse}.CacheKey"/>.</item>
///   <item>On hit  → return cached value immediately (handler is never called).</item>
///   <item>On miss → invoke the next behavior/handler, then store the result in cache.</item>
/// </list>
/// </para>
/// Pipeline position: after Logging and Validation, before Performance.
/// This ensures invalid requests never reach the cache, and that the
/// Performance behavior only measures actual handler work (cache misses).
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(
    ICacheService              cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest  : ICachedQuery<TResponse>
    where TResponse : class
{
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        var key = request.CacheKey;

        var cached = await cache.GetAsync<TResponse>(key, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache HIT  [{Request}] key={Key}", typeof(TRequest).Name, key);
            return cached;
        }

        logger.LogDebug("Cache MISS [{Request}] key={Key}", typeof(TRequest).Name, key);

        var response = await next();

        await cache.SetAsync(key, response, request.Expiration, ct);

        return response;
    }
}
