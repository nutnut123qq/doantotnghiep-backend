using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StockInvestment.Application.Interfaces;
using StockInvestment.Infrastructure.Data;
using System.Diagnostics;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for monitoring system health and performance
/// </summary>
public class SystemHealthService : ISystemHealthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SystemHealthService> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public SystemHealthService(
        ApplicationDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<SystemHealthService> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _logger = logger;
    }

    public async Task<SystemHealthStatus> GetSystemHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        var health = new SystemHealthStatus
        {
            CheckedAt = DateTime.UtcNow
        };

        try
        {
            // Check database
            var dbStopwatch = Stopwatch.StartNew();
            health.Database.IsConnected = await CheckDatabaseHealthAsync();
            health.Database.ResponseTimeMs = dbStopwatch.ElapsedMilliseconds;

            // Check cache
            var cacheStopwatch = Stopwatch.StartNew();
            health.Cache.IsConnected = await CheckCacheHealthAsync();
            health.Cache.ResponseTimeMs = cacheStopwatch.ElapsedMilliseconds;

            // Check background jobs
            var jobsStatus = await GetBackgroundJobsStatusAsync();
            health.BackgroundJobs.AllJobsRunning = jobsStatus.Jobs.All(j => j.IsRunning);
            health.BackgroundJobs.Jobs = jobsStatus.Jobs;

            // Get performance metrics
            health.Performance = await GetPerformanceMetricsAsync();

            // Overall health: only consider DB and cache since job tracking is not implemented
            health.IsHealthy = health.Database.IsConnected && 
                              health.Cache.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking system health");
            health.IsHealthy = false;
        }

        stopwatch.Stop();
        _logger.LogInformation("System health check completed in {ElapsedMs}ms. Status: {IsHealthy}", 
            stopwatch.ElapsedMilliseconds, health.IsHealthy);

        return health;
    }

    public async Task<bool> CheckDatabaseHealthAsync()
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return false;
        }
    }

    public async Task<bool> CheckCacheHealthAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache health check failed");
            return false;
        }
    }

    public Task<BackgroundJobsStatus> GetBackgroundJobsStatusAsync()
    {
        // TODO: Implement real job execution tracking (e.g. via Redis or database)
        // so health checks reflect actual job state instead of static data.
        var knownJobs = new[]
        {
            "StockPriceUpdateJob",
            "AlertMonitorJob",
            "TechnicalIndicatorCalculationJob",
            "NewsCrawlerJob",
            "EventCrawlerJob",
            "EventRssCrawlerJob"
        };

        var status = new BackgroundJobsStatus
        {
            Jobs = knownJobs.Select(name => new JobStatus
            {
                JobName = name,
                IsRunning = false,
                LastRunTime = null,
                Status = "Not tracked"
            }).ToList()
        };

        return Task.FromResult(status);
    }

    private async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
    {
        var process = Process.GetCurrentProcess();
        
        return new PerformanceMetrics
        {
            CpuUsagePercent = await GetCpuUsageAsync(process),
            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
            TotalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
            ActiveConnections = Process.GetProcesses().Length, // Simplified
            UptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds
        };
    }

    private async Task<double> GetCpuUsageAsync(Process process)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            await Task.Delay(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return Math.Round(cpuUsageTotal * 100, 2);
        }
        catch
        {
            return 0;
        }
    }
}
