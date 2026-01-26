using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// P1-2: Helper class for distributed lock in background jobs
/// </summary>
public static class JobLockHelper
{
    /// <summary>
    /// Attempts to acquire a distributed lock for a job
    /// </summary>
    /// <param name="scope">Service scope</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="logger">Logger</param>
    /// <param name="jobName">Job name (used as lock key)</param>
    /// <param name="lockExpiry">Lock expiry time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Distributed lock if acquired, null if lock disabled or already locked</returns>
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
            logger.LogDebug("Distributed lock is disabled for job: {JobName}", jobName);
            return null;
        }

        try
        {
            var lockFactory = scope.ServiceProvider.GetRequiredService<Func<IDistributedLock>>();
            var distributedLock = lockFactory();
            
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
            return null;
        }
    }
}
