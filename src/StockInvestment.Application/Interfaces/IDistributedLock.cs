namespace StockInvestment.Application.Interfaces;

/// <summary>
/// P1-2: Interface for distributed lock to prevent duplicate job execution across multiple instances
/// </summary>
public interface IDistributedLock : IDisposable
{
    /// <summary>
    /// Attempts to acquire a distributed lock
    /// </summary>
    /// <param name="key">Lock key (unique per job type)</param>
    /// <param name="expiry">Lock expiry time (should be longer than job execution time)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was acquired, false if already locked by another instance</returns>
    Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the distributed lock
    /// </summary>
    Task ReleaseAsync();
}
