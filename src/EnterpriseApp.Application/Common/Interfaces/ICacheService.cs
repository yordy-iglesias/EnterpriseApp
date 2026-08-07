namespace EnterpriseApp.Application.Common.Interfaces;

/// <summary>
/// Abstraction over HybridCache / Redis — Application declares the contract,
/// Infrastructure wires the concrete implementation.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiry = null, CancellationToken ct = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
