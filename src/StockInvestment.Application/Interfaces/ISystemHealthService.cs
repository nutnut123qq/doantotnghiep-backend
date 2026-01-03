namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service for monitoring system health
/// </summary>
public interface ISystemHealthService
{
    /// <summary>
    /// Get overall system health status
    /// </summary>
    Task<SystemHealthStatus> GetSystemHealthAsync();
    
    /// <summary>
    /// Get database connection status
    /// </summary>
    Task<bool> CheckDatabaseHealthAsync();
    
    /// <summary>
    /// Get Redis cache status
    /// </summary>
    Task<bool> CheckCacheHealthAsync();
    
    /// <summary>
    /// Get background jobs status
    /// </summary>
    Task<BackgroundJobsStatus> GetBackgroundJobsStatusAsync();
}

/// <summary>
/// Overall system health status
/// </summary>
public class SystemHealthStatus
{
    public bool IsHealthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public DatabaseHealth Database { get; set; } = new();
    public CacheHealth Cache { get; set; } = new();
    public BackgroundJobsHealth BackgroundJobs { get; set; } = new();
    public PerformanceMetrics Performance { get; set; } = new();
}

public class DatabaseHealth
{
    public bool IsConnected { get; set; }
    public int ConnectionPoolSize { get; set; }
    public long ResponseTimeMs { get; set; }
}

public class CacheHealth
{
    public bool IsConnected { get; set; }
    public long ResponseTimeMs { get; set; }
    public int CachedItemsCount { get; set; }
}

public class BackgroundJobsHealth
{
    public bool AllJobsRunning { get; set; }
    public List<JobStatus> Jobs { get; set; } = new();
}

public class JobStatus
{
    public string JobName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public DateTime? LastRunTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PerformanceMetrics
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long TotalMemoryMB { get; set; }
    public int ActiveConnections { get; set; }
    public long UptimeSeconds { get; set; }
}

/// <summary>
/// Background jobs status
/// </summary>
public class BackgroundJobsStatus
{
    public List<JobStatus> Jobs { get; set; } = new();
}
