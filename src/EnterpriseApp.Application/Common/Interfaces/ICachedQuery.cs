using MediatR;

namespace EnterpriseApp.Application.Common.Interfaces;

/// <summary>
/// Marker interface that opts a query into automatic pipeline caching.
/// Implement this on any <see cref="IRequest{TResponse}"/> to have
/// <c>CachingBehavior</c> check the cache before invoking the handler,
/// and store the result on a cache miss.
/// </summary>
/// <typeparam name="TResponse">The query response type.</typeparam>
public interface ICachedQuery<TResponse> : IRequest<TResponse>
{
    /// <summary>
    /// Unique cache key for this query instance.
    /// Should encode all parameters that affect the result.
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Optional TTL override. <c>null</c> uses the default expiration
    /// configured in <see cref="ICacheService"/>.
    /// </summary>
    TimeSpan? Expiration { get; }
}
