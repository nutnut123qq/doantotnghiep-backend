using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Services;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Helper for acquiring distributed locks in background jobs.
///
/// Contract (callers rely on this):
/// - Returns a non-null <see cref="IDistributedLock"/> when the job SHOULD run.
///   This includes the real <c>RedisDistributedLock</c> (lock successfully
///   acquired) and a <c>NoOpDistributedLock</c> (lock disabled via config, or
///   Redis unavailable — policy is to fall back to single-instance execution).
/// - Returns <c>null</c> ONLY when another instance currently holds the lock
///   (i.e. duplicate run should be skipped).
/// </summary>
public static class JobLockHelper
{
    /// <summary>
    /// Attempts to acquire a distributed lock for a job.
    /// </summary>
    /// <param name="scope">Service scope used to resolve the lock factory.</param>
    /// <param name="configuration">App configuration (reads <c>BackgroundJobs:EnableDistributedLock</c>).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="jobName">Job name, used as the lock key suffix.</param>
    /// <param name="lockExpiry">Lock TTL. Must exceed the worst-case job duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A disposable lock when the job should proceed; <c>null</c> only when the
    /// lock is held by another instance.
    /// </returns>
    public static async Task<IDistributedLock?> TryAcquireLockAsync(
        IServiceScope scope,
        IConfiguration configuration,
        ILogger logger,
        string jobName,
        TimeSpan lockExpiry,
        CancellationToken cancellationToken = default)
    {
        var lockEnabled = configuration.GetValue<bool>("BackgroundJobs:EnableDistributedLock", defaultValue: true);
        if (!lockEnabled)
        {
            logger.LogDebug("Distributed lock is disabled for job: {JobName}. Running with no-op lock.", jobName);
            return new NoOpDistributedLock();
        }

        IDistributedLock? distributedLock = null;
        try
        {
            var lockFactory = scope.ServiceProvider.GetRequiredService<Func<IDistributedLock>>();
            distributedLock = lockFactory();

            var lockKey = $"job:{jobName}";
            var acquired = await distributedLock.TryAcquireAsync(lockKey, lockExpiry, cancellationToken);

            if (!acquired)
            {
                logger.LogInformation("{JobName} skipped - already running on another instance", jobName);
                distributedLock.Dispose();
                return null;
            }

            logger.LogDebug("Acquired distributed lock for job: {JobName}", jobName);
            return distributedLock;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to acquire distributed lock for job: {JobName}, continuing without lock", jobName);
            try
            {
                distributedLock?.Dispose();
            }
            catch
            {
                // best-effort cleanup; swallowing is safe here.
            }
            return new NoOpDistributedLock();
        }
    }
}
