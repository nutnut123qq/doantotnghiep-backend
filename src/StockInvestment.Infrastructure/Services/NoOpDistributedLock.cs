using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// No-op distributed lock used when EnableDistributedLock=false or when the
/// Redis backing store is unavailable. The job proceeds as if the lock was
/// acquired. Chosen for single-instance/dev deployments where correctness
/// does not require multi-instance mutual exclusion.
/// </summary>
public sealed class NoOpDistributedLock : IDistributedLock
{
    public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task ReleaseAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}
