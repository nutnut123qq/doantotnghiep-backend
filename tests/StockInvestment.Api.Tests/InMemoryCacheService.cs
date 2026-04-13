using System.Collections.Concurrent;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Api.Tests;

/// <summary>
/// In-memory implementation of ICacheService for integration tests (no Redis).
/// </summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, (string Value, DateTime? Expires)> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (_store.TryGetValue(key, out var entry) && (entry.Expires == null || entry.Expires > DateTime.UtcNow))
            return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<T>(entry.Value));
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        var expires = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null;
        _store[key] = (System.Text.Json.JsonSerializer.Serialize(value), expires);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var wildcard = pattern.Replace("*", "");
        foreach (var k in _store.Keys.ToList())
        {
            if (k.Contains(wildcard, StringComparison.OrdinalIgnoreCase))
                _store.TryRemove(k, out _);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var exists = _store.TryGetValue(key, out var entry) && (entry.Expires == null || entry.Expires > DateTime.UtcNow);
        return Task.FromResult(exists);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        var existing = await GetAsync<T>(key, cancellationToken);
        if (existing != null) return existing;
        var value = await factory();
        await SetAsync(key, value, expiration, cancellationToken);
        return value;
    }

    public Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var newVal = _store.AddOrUpdate(key,
            _ => ("1", expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null),
            (_, entry) =>
            {
                var n = long.TryParse(entry.Value, out var v) ? v + 1 : 1;
                return (n.ToString(), entry.Expires);
            });
        return Task.FromResult(long.TryParse(newVal.Value, out var num) ? num : 1);
    }
}
